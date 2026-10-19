namespace Companion.Data.Models;

/// <summary>
/// One-time password row. Only the HMAC-SHA256 code hash is stored here.
/// The delivery outbox carries the plaintext code until the notification is delivered.
/// </summary>
public sealed class OtpCode
{
    public int Id { get; set; }

    public int ProfileId { get; set; }

    public Profile? Profile { get; set; }

    /// <summary>Persisted as a string: <see cref="OtpPurpose.EmailVerification"/> or
    /// <see cref="OtpPurpose.PasswordReset"/>.</summary>
    public OtpPurpose Purpose { get; set; } = OtpPurpose.EmailVerification;

    public byte[] CodeHash { get; set; } = null!;

    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset? ConsumedAt { get; set; }

    public int Attempts { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
