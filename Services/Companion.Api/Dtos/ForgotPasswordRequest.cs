namespace Companion.Api.Dtos;

/// <summary>Payload for <c>POST /api/auth/forgot-password</c>.</summary>
public sealed class ForgotPasswordRequest
{
    public string Email { get; init; } = string.Empty;
}
