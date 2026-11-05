namespace Companion.Api.Dtos;

/// <summary>Payload for <c>POST /api/auth/google</c>.</summary>
public sealed class GoogleLoginRequest
{
    public string IdToken { get; init; } = string.Empty;

    /// <summary>Required only when Google sign-in creates a new account.</summary>
    public bool PopiaConsent { get; init; }
}
