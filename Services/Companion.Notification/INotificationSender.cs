namespace Companion.Notification;

/// <summary>Completes only after the delivery provider accepts the notification.</summary>
public interface INotificationSender
{
    Task SendAsync(Guid eventId, RenderedNotification notification, CancellationToken cancellationToken = default);
}
