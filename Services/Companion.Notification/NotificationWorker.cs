namespace Companion.Notification;

public sealed class NotificationWorker(ILogger<NotificationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Companion.Notification worker started.");

        // Idles until shutdown; consumers arrive with the first notification feature.
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }
}
