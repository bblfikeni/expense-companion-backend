namespace Companion.Api.Dtos;

/// <summary>Payload for <c>POST /api/auth/verify-email</c>.</summary>
public sealed class VerifyEmailRequest
{
    public string Email { get; init; } = string.Empty;

    public string Code { get; init; } = string.Empty;
}
