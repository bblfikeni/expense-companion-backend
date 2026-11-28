using Microsoft.AspNetCore.Identity;

namespace Companion.Data.Models;

/// <summary>
/// An application user.
/// <para>
/// <see cref="IdentityUser{TKey}.Id"/> is an internal surrogate key only: it is
/// an int, it is never serialised to API clients, and every externally visible
/// identifier is <see cref="PublicId"/>.
/// </para>
/// </summary>
public class Profile : IdentityUser<int>
{
    /// <summary>Display name, maximum 255 characters, never null.</summary>
    public required string FullName { get; set; }

    /// <summary>
    /// POPIA consent flag. The database has no CHECK constraint on consent —
    /// consent is deliberately modelled as an ordinary value so it can be
    /// recorded, revoked and anonymised without a schema change.
    /// </summary>
    public bool PopiaConsentGiven { get; set; }

    /// <summary>When <see cref="PopiaConsentGiven"/> was recorded. Mapped to timestamptz.</summary>
    public DateTimeOffset PopiaConsentDate { get; set; }

    /// <summary>True once the personal data behind this profile has been irreversibly anonymised.</summary>
    public bool IsAnonymized { get; set; }

    /// <summary>Stable public identifier exposed by the API. Database default: gen_random_uuid().</summary>
    public Guid PublicId { get; set; }
}
