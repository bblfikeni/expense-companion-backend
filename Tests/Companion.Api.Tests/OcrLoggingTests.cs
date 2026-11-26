using Companion.Extraction.Ocr;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Companion.Api.Tests;

public class OcrLoggingTests
{
    [Fact]
    public async Task PlaceholderReportsProgressWithoutLoggingImagePath()
    {
        var logger = new RecordingLogger();
        var engine = new TesseractOcrEngine(logger: logger);

        var result = await engine.ExtractAsync("sensitive-receipt-path.jpg");

        Assert.Empty(result.Text);
        Assert.Contains(logger.Messages, message => message.Contains("started"));
        Assert.Contains(logger.Messages, message => message.Contains("not implemented"));
        Assert.DoesNotContain(logger.Messages, message => message.Contains("sensitive-receipt-path"));
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