namespace Companion.Api.Dtos;

/// <summary>Payload for <c>POST /api/auth/register</c>.</summary>
public sealed class RegisterRequest
{
    public string Email { get; init; } = string.Empty;

    public string Password { get; init; } = string.Empty;

    public string FullName { get; init; } = string.Empty;

    /// <summary>POPIA consent flag. Must be <c>true</c> or the request is rejected.</summary>
    public bool PopiaConsent { get; init; }
}
