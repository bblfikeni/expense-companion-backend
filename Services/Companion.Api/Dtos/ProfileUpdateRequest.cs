namespace Companion.Api.Dtos;

/// <summary>Payload for <c>PUT /api/profile</c>.</summary>
public sealed class ProfileUpdateRequest
{
    public string FullName { get; init; } = string.Empty;
}
