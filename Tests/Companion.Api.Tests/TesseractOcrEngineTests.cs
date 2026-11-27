using System.Diagnostics;
using System.Drawing;
using Companion.Events;
using Companion.Extraction;
using Companion.Extraction.Configuration;
using Companion.Extraction.Ocr;
using Microsoft.Extensions.Logging.Abstractions;

namespace Companion.Api.Tests;

public sealed class TesseractOcrEngineTests : IDisposable
{
    private readonly DirectoryInfo _directory = Directory.CreateTempSubdirectory("companion-test-ocr-");

    [UnixFact]
    public async Task ReadsStdinAndParsesTsvWithNormalizedBoxes()
    {
        using var engine = CreateEngine("""
            [ "$*" = "stdin stdout -l eng+afr --psm 6 tsv" ] || exit 8
            [ "$OMP_THREAD_LIMIT" = "1" ] || exit 9
            [ "$(cat)" = "synthetic input" ] || exit 10
            printf 'level\tpage_num\tblock_num\tpar_num\tline_num\tword_num\tleft\ttop\twidth\theight\tconf\ttext\n'
            printf '1\t1\t0\t0\t0\t0\t0\t0\t200\t100\t-1\t\n'
            printf '5\t1\t1\t1\t1\t1\t20\t10\t40\t10\t95.5\tDemo\n'
            printf '5\t1\t1\t1\t1\t2\t64\t10\t20\t10\t84.5\tMarket\n'
            printf '5\t1\t2\t1\t1\t1\t20\t25\t60\t10\t60.0\tTotal\n'
            """);

        var result = await engine.ExtractAsync("synthetic input"u8.ToArray()).WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal("Demo Market\nTotal", result.Text);
        Assert.Equal(0.80, result.MeanConfidence, 3);
        var line = result.Lines.First();
        Assert.Equal(1, line.PageNo);
        Assert.Equal(0.90, line.Confidence, 3);
        var word = line.Words.First();
        Assert.Equal(0.955, word.Confidence, 3);
        var box = word.BoundingBox;
        Assert.Equal(new RectangleF(0.1f, 0.1f, 0.2f, 0.1f),
            new RectangleF(Round(box.X), Round(box.Y), Round(box.Width), Round(box.Height)));
    }

    [UnixFact]
    public async Task TimeoutKillsParentAndChildAndReturnsTypedFailure()
    {
        using var engine = CreateEngine("""
            cd "$(dirname "$0")"
            sleep 600 &
            printf '%s %s' "$$" "$!" > pids
            wait
            """, timeoutSeconds: 1);

        await Assert.ThrowsAsync<OcrTimeout>(() =>
            engine.ExtractAsync([1]).WaitAsync(TimeSpan.FromSeconds(5)));

        await AssertProcessesExitedAsync(Path.Combine(_directory.FullName, "pids"));
    }

    [UnixFact]
    public async Task MessageDeadlineKillsAllTenHangingOcrInvocations()
    {
        const int images = 10;
        const int seconds = 3;
        var settings = new ExtractionOptions
        {
            Enabled = true, MessageTimeoutSeconds = seconds, OcrMaxConcurrency = images,
        };
        using var engine = CreateEngine("""
            cd "$(dirname "$0")"
            sleep 600 &
            printf '%s %s' "$$" "$!" > "pids-$$"
            wait
            """, settings);
        var bus = new ExtractionWorkerTests.RecordingBus();
        using var worker = new ExtractionWorker(bus, new ParallelOcrProcessor(engine, images),
            Microsoft.Extensions.Options.Options.Create(settings), NullLogger<ExtractionWorker>.Instance);
        using var watchdog = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await worker.StartAsync(watchdog.Token);
        try
        {
            await bus.Subscribed.Task.WaitAsync(watchdog.Token);
            var stopwatch = Stopwatch.StartNew();
            await Assert.ThrowsAsync<ExtractionMessageTimeout>(() => bus.DeliverAsync(
                new ReceiptUploadedEvent { EventId = Guid.NewGuid(), ExpensePublicId = Guid.NewGuid() }, watchdog.Token)
                .WaitAsync(TimeSpan.FromSeconds(seconds + 2)));
            Assert.InRange(stopwatch.Elapsed, TimeSpan.FromSeconds(seconds), TimeSpan.FromSeconds(seconds + 2));

            var pidFiles = _directory.GetFiles("pids-*");
            Assert.Equal(images, pidFiles.Length);
            await Task.WhenAll(pidFiles.Select(file => AssertProcessesExitedAsync(file.FullName)));
        }
        finally
        {
            watchdog.Cancel();
            await worker.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(3));
        }
    }

    [UnixFact]
    public async Task NonzeroExitReportsCodeWithoutStderrOrStdout()
    {
        using var engine = CreateEngine("""
            printf 'synthetic private receipt text'
            printf 'synthetic private filename' >&2
            exit 17
            """);

        var error = await Assert.ThrowsAsync<OcrProcessException>(() =>
            engine.ExtractAsync([1]).WaitAsync(TimeSpan.FromSeconds(5)));

        Assert.Equal("Tesseract exited with code 17.", error.Message);
    }

    [UnixFact]
    public async Task ConcurrencyNeverStartsMoreThanConfiguredNumberOfProcesses()
    {
        using var engine = CreateEngine("""
            cd "$(dirname "$0")"
            touch "entered-$$"
            while [ ! -f release ]; do sleep 0.05; done
            cat > /dev/null
            """);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var calls = Enumerable.Range(0, 5).Select(_ => engine.ExtractAsync([1], deadline.Token)).ToArray();
        try
        {
            while (_directory.GetFiles("entered-*").Length < 2)
                await Task.Delay(20, deadline.Token);
            await Task.Delay(150, deadline.Token);
            Assert.Equal(2, _directory.GetFiles("entered-*").Length);
        }
        finally
        {
            await File.WriteAllTextAsync(Path.Combine(_directory.FullName, "release"), "");
            await Task.WhenAll(calls).WaitAsync(TimeSpan.FromSeconds(11));
        }

        Assert.Equal(5, _directory.GetFiles("entered-*").Length);
    }

    private TesseractOcrEngine CreateEngine(string script, int timeoutSeconds = 30)
        => CreateEngine(script, new ExtractionOptions { OcrTimeoutSeconds = timeoutSeconds });

    private TesseractOcrEngine CreateEngine(string script, ExtractionOptions options)
    {
        var executable = Path.Combine(_directory.FullName, "tesseract");
        File.WriteAllText(executable, "#!/bin/sh\n" + script + "\n");
        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(executable, UnixFileMode.UserRead | UnixFileMode.UserExecute);
        options.OcrExecutablePath = executable;
        return new TesseractOcrEngine(Microsoft.Extensions.Options.Options.Create(options),
            NullLogger<TesseractOcrEngine>.Instance);
    }

    private static async Task AssertProcessesExitedAsync(string pidFile)
    {
        var pids = (await File.ReadAllTextAsync(pidFile)).Split(' ');
        Assert.Equal(2, pids.Length);
        foreach (var pid in pids)
        {
            using var status = Process.Start(new ProcessStartInfo("ps", $"-p {int.Parse(pid)} -o state=")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
            })!;
            var state = (await status.StandardOutput.ReadToEndAsync().WaitAsync(TimeSpan.FromSeconds(2))).Trim();
            // A killed child can remain a zombie briefly until its new parent reaps it.
            Assert.True(state.Length == 0 || state.StartsWith('Z'), $"Process {pid} remained live ({state}).");
        }
    }

    private sealed class ParallelOcrProcessor(IOcrEngine engine, int imageCount) : IReceiptExtractionProcessor
    {
        public Task ProcessAsync(ReceiptUploadedEvent message, CancellationToken cancellationToken) =>
            Task.WhenAll(Enumerable.Range(0, imageCount).Select(_ => engine.ExtractAsync([1], cancellationToken)));
    }

    private static float Round(float value) => MathF.Round(value, 3);

    public void Dispose() => _directory.Delete(recursive: true);

    public sealed class UnixFactAttribute : FactAttribute
    {
        public UnixFactAttribute()
        {
            if (OperatingSystem.IsWindows())
                Skip = "The fake executable uses a Unix shell.";
        }
    }
}
