namespace Companion.Api.Dtos;

/// <summary>Payload for POST /api/auth/onboard.</summary>
public sealed class OnboardRequest
{
    /// <summary>Profile public ID.</summary>
    public required Guid ProfilePublicId { get; init; }

    /// <summary>Display name.</summary>
    public required string FullName { get; init; }

    /// <summary>POPIA consent given.</summary>
    public required bool ConsentGiven { get; init; }
}