using System.Security.Cryptography;

namespace Companion.Data;

/// <summary>
/// Tuning knobs for the OTP service. Every value is configurable from
/// <c>Auth:Otps</c> (or the equivalent host-specific configuration).
/// </summary>
public sealed class OtpOptions
{
    /// <summary>How long a code stays valid, in minutes. Default 10.</summary>
    public int ExpiresInMinutes { get; set; } = 10;

    /// <summary>Wrong guesses before an OTP of a purpose is locked. Default 5.</summary>
    public int MaxAttempts { get; set; } = 5;

    /// <summary>Minimum interval between consecutive codes for the same email.
    /// Codes for unconfirmed profiles still respect this. Default 60s.</summary>
    public int ResendCooldownInSeconds { get; set; } = 60;
}
