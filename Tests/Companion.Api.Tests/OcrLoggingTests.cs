using Companion.Extraction.Ocr;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Companion.Api.Tests;

public class OcrLoggingTests
{
    [Fact]
    public async Task FailedExtractionLogsProgressWithoutLoggingImagePath()
    {
        var logger = new RecordingLogger();
        var engine = new TesseractOcrEngine(logger: logger);
        const string imagePath = "sensitive-receipt-path.jpg";

        await Assert.ThrowsAnyAsync<Exception>(() => engine.ExtractAsync(imagePath));

        Assert.Contains(logger.Messages, message => message.Contains("started"));
        Assert.Contains(logger.Messages, message => message.Contains("failed"));
        Assert.DoesNotContain(logger.Messages, message => message.Contains(imagePath));
    }

    [Fact]
    public async Task PreCanceledExtractionDoesNotInitializeNativeEngine()
    {
        var logger = new RecordingLogger();
        var engine = new TesseractOcrEngine(logger: logger);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            engine.ExtractAsync("sensitive-receipt-path.jpg", cancellation.Token));

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