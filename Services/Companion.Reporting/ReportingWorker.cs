namespace Companion.Reporting;

public sealed class ReportingWorker(ILogger<ReportingWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Companion.Reporting worker started.");

        // Idles until shutdown; jobs arrive with the first reporting feature.
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }
}
