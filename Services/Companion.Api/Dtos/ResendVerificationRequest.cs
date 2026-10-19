namespace Companion.Api.Dtos;

/// <summary>Payload for <c>POST /api/auth/resend-verification</c>.</summary>
public sealed class ResendVerificationRequest
{
    public string Email { get; init; } = string.Empty;
}
