namespace Companion.Scheduler;

public sealed class SchedulerWorker(ILogger<SchedulerWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Companion.Scheduler worker started.");

        // Idles until shutdown; jobs arrive with the first scheduling feature.
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }
}
