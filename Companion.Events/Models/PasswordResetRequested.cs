namespace Companion.Events;

/// <summary>
/// Fired by <see cref="AuthenticationService"/> when a confirmed profile requests a password
/// reset.
/// <para>
/// This is the sanctioned exception to the "identifiers-only" rule: auth events carry email
/// and the OTP in plaintext (the notification channel needs them to deliver the code). They
/// are still written ONLY through the transactional outbox and are never logged, returned to
/// clients, or exposed in tokens.
/// </para>
/// </summary>
public sealed class PasswordResetRequested
{
    public Guid ProfilePublicId { get; init; }

    public string Email { get; init; } = string.Empty;

    public string OtpCode { get; init; } = string.Empty;

    public DateTimeOffset ExpiresAtUtc { get; init; }

    public Guid EventId { get; init; }

    public DateTimeOffset OccurredAtUtc { get; init; }

    public string? CorrelationId { get; init; }
}
