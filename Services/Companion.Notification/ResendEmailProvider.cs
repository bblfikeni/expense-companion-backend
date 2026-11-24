using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Options;

namespace Companion.Notification;

internal sealed class ResendEmailProvider(HttpClient client, IOptions<ResendOptions> options) : INotificationSender
{
    public async Task SendAsync(Guid eventId, RenderedNotification notification, CancellationToken cancellationToken = default)
    {
        if (eventId == Guid.Empty)
            throw new ArgumentException("A notification event ID is required.", nameof(eventId));

        using var request = new HttpRequestMessage(HttpMethod.Post, "emails")
        {
            Content = JsonContent.Create(new
            {
                from = options.Value.From,
                to = new[] { notification.Recipient },
                subject = notification.Subject,
                html = notification.HtmlBody,
                text = notification.TextBody,
            }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.Value.ApiKey);
        request.Headers.Add("Idempotency-Key", eventId.ToString("D"));

        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Email delivery failed with HTTP status {(int)response.StatusCode}.",
                inner: null, response.StatusCode);
        }
    }
}
