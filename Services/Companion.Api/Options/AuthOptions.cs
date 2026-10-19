using Companion.Data;

namespace Companion.Api.Options;

/// <summary>
/// Top-level authentication configuration, bound from the <c>Auth</c> configuration
/// section (with <c>:</c> replaced by <c>__</c> for environment variables).
/// </summary>
public sealed class AuthOptions
{
    /// <summary>OTP service tuning (expiry, attempt cap, resend cooldown).</summary>
    public OtpOptions Otps { get; set; } = new();

    /// <summary>JWT bearer signing and validation parameters.</summary>
    public JwtOptions Jwt { get; set; } = new();
}

/// <summary>
/// JWT bearer signing key (base64-encoded, at least 32 bytes = 256 bits), plus the
/// issuer and audience for token validation.
/// </summary>
public sealed class JwtOptions
{
    public string Issuer { get; set; } = string.Empty;

    public string Audience { get; set; } = string.Empty;

    public string SigningKey { get; set; } = string.Empty;
}
