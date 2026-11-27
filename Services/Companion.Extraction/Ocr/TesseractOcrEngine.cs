using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using Companion.Extraction.Configuration;
using Microsoft.Extensions.Options;

namespace Companion.Extraction.Ocr;

/// <summary>Runs Tesseract in a child process, with receipt data confined to pipes.</summary>
public sealed class TesseractOcrEngine(
    IOptions<ExtractionOptions> options,
    ILogger<TesseractOcrEngine> logger) : IOcrEngine, IDisposable
{
    private readonly ExtractionOptions _options = options.Value;
    private readonly SemaphoreSlim _slots = new(options.Value.OcrMaxConcurrency);

    public async Task<OcrResult> ExtractAsync(byte[] imageBytes, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(imageBytes);
        ArgumentOutOfRangeException.ThrowIfZero(imageBytes.Length);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(_options.OcrTimeoutSeconds));
        try
        {
            await _slots.WaitAsync(timeout.Token);
            try
            {
                return await RunAsync(imageBytes, timeout.Token);
            }
            finally
            {
                _slots.Release();
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("OCR timed out.");
            throw new OcrTimeout();
        }
    }

    private async Task<OcrResult> RunAsync(byte[] imageBytes, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var start = new ProcessStartInfo(_options.OcrExecutablePath)
        {
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var argument in new[] { "stdin", "stdout", "-l", "eng+afr", "--psm",
                     _options.OcrPageSegmentationMode.ToString(CultureInfo.InvariantCulture), "tsv" })
            start.ArgumentList.Add(argument);
        start.Environment["OMP_THREAD_LIMIT"] = "1";
        using var process = new Process { StartInfo = start };
        try
        {
            process.Start();
        }
        catch (Win32Exception)
        {
            throw new OcrProcessException("Could not start Tesseract.");
        }

        var output = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errorDrain = process.StandardError.BaseStream.CopyToAsync(Stream.Null, cancellationToken);
        var input = WriteImageAsync(process.StandardInput.BaseStream, imageBytes, cancellationToken);
        var completion = Task.WhenAll(output, errorDrain, input, process.WaitForExitAsync(cancellationToken));
        try
        {
            await completion.WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException) when (process.HasExited)
            {
                // The child exited as cancellation raced process shutdown.
            }
            process.WaitForExit(milliseconds: 1000);
            throw;
        }

        if (process.ExitCode != 0)
            throw new OcrProcessException($"Tesseract exited with code {process.ExitCode}.");
        if (!await input)
            throw new OcrProcessException("Tesseract closed its input before receiving the image.");

        return TesseractTsv.Parse(await output, cancellationToken);
    }

    private static async Task<bool> WriteImageAsync(Stream input, byte[] imageBytes, CancellationToken cancellationToken)
    {
        try
        {
            using (input)
                await input.WriteAsync(imageBytes, cancellationToken);
            return true;
        }
        catch (IOException)
        {
            // An early exit can close stdin. The exit code is checked after the pipes drain.
            return false;
        }
    }

    public void Dispose() => _slots.Dispose();
}

public sealed class OcrTimeout() : TimeoutException("Tesseract exceeded the configured OCR timeout.");

public sealed class OcrProcessException(string message) : Exception(message);
