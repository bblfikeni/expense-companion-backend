namespace Companion.Data.Interfaces;

/// <summary>Outcome of an OTP verification attempt.</summary>
public sealed class OtpVerifyResult
{
    public bool Success { get; init; }

    public OtpValidationFailure Failure { get; init; }

    public int RemainingAttempts { get; init; }

    public DateTimeOffset ExpiresAtUtc { get; init; }
}

/// <summary>Why a verification attempt failed.</summary>
public enum OtpValidationFailure
{
    /// <summary>No code issued for this profile/email.</summary>
    Invalid,

    /// <summary>The OTP has expired.</summary>
    Expired,

    /// <summary>The provided code did not match the stored hash.</summary>
    CodeMismatch,
}
