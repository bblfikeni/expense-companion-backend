using System.Globalization;
using System.Net;
using Companion.Events;

namespace Companion.Notification;

public sealed record RenderedNotification(string Recipient, string Subject, string HtmlBody, string TextBody);

public sealed class NotificationTemplateRenderer
{
    private readonly string _templatesPath;

    public NotificationTemplateRenderer(IHostEnvironment environment)
        => _templatesPath = Path.Combine(environment.ContentRootPath, "Templates");

    public RenderedNotification Render(EmailVerificationRequested request)
        => Render(
            request.Email,
            "Verify your email address",
            "EmailVerificationRequested.html",
            "EmailVerificationRequested.txt",
            request.OtpCode,
            request.ExpiresAtUtc);

    public RenderedNotification Render(PasswordResetRequested request)
        => Render(
            request.Email,
            "Reset your password",
            "PasswordResetRequested.html",
            "PasswordResetRequested.txt",
            request.OtpCode,
            request.ExpiresAtUtc);

    private RenderedNotification Render(
        string recipient,
        string subject,
        string htmlTemplate,
        string textTemplate,
        string otpCode,
        DateTimeOffset expiresAtUtc)
    {
        var expires = expiresAtUtc.ToUniversalTime().ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture);
        var html = File.ReadAllText(Path.Combine(_templatesPath, htmlTemplate))
            .Replace("{{OtpCode}}", WebUtility.HtmlEncode(otpCode), StringComparison.Ordinal)
            .Replace("{{ExpiresAtUtc}}", WebUtility.HtmlEncode(expires), StringComparison.Ordinal);
        var text = File.ReadAllText(Path.Combine(_templatesPath, textTemplate))
            .Replace("{{OtpCode}}", otpCode, StringComparison.Ordinal)
            .Replace("{{ExpiresAtUtc}}", expires, StringComparison.Ordinal);

        return new RenderedNotification(recipient, subject, html, text);
    }
}
