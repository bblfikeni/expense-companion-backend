using System.ComponentModel.DataAnnotations;

namespace Companion.Data.Models;

/// <summary>
/// Persistent refresh token for session management.
/// Each user can have multiple tokens, but only the most recent (by creation time)
/// is considered valid. Older tokens are invalidated on every successful login
/// and on token rotation during refresh.
/// </summary>
public sealed class RefreshToken
{
    /// <summary>Primary key.</summary>
    public int Id { get; set; }

    /// <summary>Foreign key to the user profile.</summary>
    public int ProfileId { get; set; }

    /// <summary>The user.</summary>
    public Profile? Profile { get; set; }

    /// <summary>Server-generated random value (base64-encoded, 64 bytes = 512 bits).</summary>
    public required string Token { get; set; }

    /// <summary>When the token was created.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>When the token expires.</summary>
    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>True if the token has been consumed/revoked.</summary>
    public bool Consumed { get; set; }

    /// <summary>When the token was consumed/revoked.</summary>
    public DateTimeOffset? ConsumedAt { get; set; }
}