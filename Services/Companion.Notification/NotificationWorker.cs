using Companion.Events;
using Companion.Messaging;

namespace Companion.Notification;

public sealed class NotificationWorker(
    ILogger<NotificationWorker> logger,
    ICompanionMessageBus bus,
    NotificationTemplateRenderer templates,
    IEnumerable<INotificationSender> senders,
    TimeProvider clock) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Companion.Notification worker started.");

        var sender = senders.SingleOrDefault();
        if (sender is null)
        {
            logger.LogWarning("""No notification sender is registered. Consumption is disabled; delivery events remain pending.""");
            return;
        }

        await using var emailVerificationSubscription = await bus.SubscribeAsync<EmailVerificationRequested>(
            "companion-notification-email-verification",
            (request, ct) => ProcessAsync(sender, request.EventId, request.ExpiresAtUtc, () => templates.Render(request), ct),
            stoppingToken);
        await using var passwordResetSubscription = await bus.SubscribeAsync<PasswordResetRequested>(
            "companion-notification-password-reset",
            (request, ct) => ProcessAsync(sender, request.EventId, request.ExpiresAtUtc, () => templates.Render(request), ct),
            stoppingToken);

        logger.LogInformation(" Subscribed to email verification and password reset events.");
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private async Task ProcessAsync(
        INotificationSender sender, Guid eventId, DateTimeOffset expiresAtUtc,
        Func<RenderedNotification> render, CancellationToken ct)
    {
        if (expiresAtUtc <= clock.GetUtcNow())
        {
            logger.LogInformation("Skipped expired notification event {EventId}.", eventId);
            return;
        }

        RenderedNotification notification;
        try
        {
            notification = render();
            await sender.SendAsync(eventId, notification, ct);
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning("Notification delivery failed for event {EventId}.", eventId);
            throw;
        }
        logger.LogInformation(
            "Delivered notification event {EventId} with subject {Subject}.",
            eventId,
            notification.Subject);
    }
}
