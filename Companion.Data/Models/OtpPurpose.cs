namespace Companion.Data.Models;

/// <summary>
/// Purpose of an OTP row, persisted to the database as a string (never an enum payload).
/// </summary>
public enum OtpPurpose
{
    /// <summary>Issued at registration; consumed by <c>POST /api/auth/verify-email</c>.</summary>
    EmailVerification,

    /// <summary>Issued by <c>POST /api/auth/forgot-password</c>; consumed by
    /// <c>POST /api/auth/reset-password</c>.</summary>
    PasswordReset,
}
