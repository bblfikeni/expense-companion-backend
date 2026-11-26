using System.Net.Mail;
using System.Net.Mime;
using System.Text;

namespace Companion.Notification;

internal sealed class DevelopmentEmailPickupSender(string pickupDirectory) : INotificationSender
{
    public async Task SendAsync(Guid eventId, RenderedNotification notification, CancellationToken cancellationToken = default)
    {
        DevelopmentEmailPickupDirectory.EnsurePrivate(pickupDirectory);

        using var message = new MailMessage(
            "Expense Companion <auth@companion.test>", notification.Recipient, notification.Subject, notification.TextBody);
        message.Headers.Add("X-Companion-Event-Id", eventId.ToString("D"));
        message.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(
            notification.HtmlBody, Encoding.UTF8, MediaTypeNames.Text.Html));

        using var smtp = new SmtpClient
        {
            DeliveryMethod = SmtpDeliveryMethod.SpecifiedPickupDirectory,
            PickupDirectoryLocation = pickupDirectory,
        };
        await smtp.SendMailAsync(message, cancellationToken);
    }
}
