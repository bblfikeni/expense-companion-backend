using Companion.Extraction.Configuration;
using Companion.Extraction.Ocr;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Companion.Api.Tests;

public class OcrLoggingTests
{
    [Fact]
    public async Task FailedProcessStartDoesNotExposeExecutablePath()
    {
        var logger = new RecordingLogger();
        var path = Path.Combine(Path.GetTempPath(), "sensitive-receipt-path", Guid.NewGuid().ToString());
        using var engine = new TesseractOcrEngine(Microsoft.Extensions.Options.Options.Create(new ExtractionOptions
        {
            OcrExecutablePath = path,
        }), logger);

        var error = await Assert.ThrowsAsync<OcrProcessException>(() => engine.ExtractAsync([1]));

        Assert.DoesNotContain(path, error.ToString());
        Assert.DoesNotContain(logger.Messages, message => message.Contains(path));
    }

    [Fact]
    public async Task PreCanceledExtractionDoesNotStartTheOcrProcess()
    {
        var logger = new RecordingLogger();
        using var engine = new TesseractOcrEngine(Microsoft.Extensions.Options.Options.Create(new ExtractionOptions()), logger);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => engine.ExtractAsync([1], cancellation.Token));

        Assert.Empty(logger.Messages);
    }

    private sealed class RecordingLogger : ILogger<TesseractOcrEngine>
    {
        public List<string> Messages { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => Messages.Add(formatter(state, exception));
    }
}
