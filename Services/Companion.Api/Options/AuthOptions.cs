using Companion.Data;

namespace Companion.Api.Options;

/// <summary>
/// Top-level authentication configuration, bound from the <c>Auth</c> configuration
/// section (with <c>:</c> replaced by <c>__</c> for environment variables).
/// </summary>
public sealed class AuthOptions
{
    /// <summary>Optional Google OAuth configuration. A blank client ID disables Google sign-in.</summary>
    public GoogleOptions Google { get; set; } = new();

    /// <summary>OTP service tuning (expiry, attempt cap, resend cooldown).</summary>
    public OtpOptions Otps { get; set; } = new();

    /// <summary>JWT bearer signing and validation parameters.</summary>
    public JwtOptions Jwt { get; set; } = new();

    /// <summary>Cloudflare Turnstile configuration. Blank site key disables Turnstile.</summary>
    public TurnstileOptions Turnstile { get; set; } = new();
}

public sealed class TurnstileOptions
{
    /// <summary>Site key for Turnstile widget (public key for client-side validation).</summary>
    public string SiteKey { get; set; } = string.Empty;

    /// <summary>Secret key for server-side Turnstile validation.</summary>
    public string SecretKey { get; set; } = string.Empty;

    /// <summary>Timeout in seconds to wait for Turnstile API response.</summary>
    public int RequestTimeoutSeconds { get; set; } = 10;

    /// <summary>Minimum score threshold for valid Turnstile responses.</summary>
    public float ScoreThreshold { get; set; } = 0.5f;
}

public sealed class GoogleOptions
{
    public string ClientId { get; set; } = string.Empty;
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
