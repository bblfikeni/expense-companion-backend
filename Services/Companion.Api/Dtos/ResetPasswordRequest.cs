namespace Companion.Api.Dtos;

/// <summary>Payload for <c>POST /api/auth/reset-password</c>.</summary>
public sealed class ResetPasswordRequest
{
    public string Email { get; init; } = string.Empty;

    public string Code { get; init; } = string.Empty;

    public string NewPassword { get; init; } = string.Empty;
}
