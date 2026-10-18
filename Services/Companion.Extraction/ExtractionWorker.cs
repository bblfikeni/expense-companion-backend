namespace Companion.Extraction;

public sealed class ExtractionWorker(ILogger<ExtractionWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Companion.Extraction worker started.");

        // Idles until shutdown; consumers arrive with the first extraction feature.
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }
}
