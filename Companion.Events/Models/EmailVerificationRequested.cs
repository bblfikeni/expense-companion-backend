namespace Companion.Events;

/// <summary>
/// Fired by <see cref="AuthenticationService"/> when a profile requests email verification.
/// <para>
/// This is the sanctioned exception to the "identifiers-only" rule: auth events are the one
/// place where email and the OTP are carried in plaintext, because the notification channel
/// (which does not exist in this slice) needs them to send the code. They are still written
/// ONLY through the transactional outbox and are never logged, returned to clients, or exposed
/// in tokens.
/// </para>
/// </summary>
public sealed class EmailVerificationRequested
{
    public Guid ProfilePublicId { get; init; }

    public string Email { get; init; } = string.Empty;

    public string OtpCode { get; init; } = string.Empty;

    public DateTimeOffset ExpiresAtUtc { get; init; }

    public Guid EventId { get; init; }

    public DateTimeOffset OccurredAtUtc { get; init; }

    public string? CorrelationId { get; init; }
}
