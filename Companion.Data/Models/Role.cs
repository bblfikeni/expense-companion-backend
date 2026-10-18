using Microsoft.AspNetCore.Identity;

namespace Companion.Data.Models;

/// <summary>
/// An RBAC role. Authorisation is expressed as permission codes carried by role
/// claims (claim type <c>permission</c>), never as a boolean flag on a profile.
/// <para>
/// Deliberately has no <c>PublicId</c>: roles are referenced by name/permission
/// code, not by a wire identifier.
/// </para>
/// </summary>
public class Role : IdentityRole<int>
{
    /// <summary>Grants every <c>platform:*</c> permission.</summary>
    public const string PlatformAdmin = "platform-admin";

    /// <summary>Claim type used for permission codes.</summary>
    public const string PermissionClaimType = "permission";

    /// <summary>Permission code held by <see cref="PlatformAdmin"/>.</summary>
    public const string PlatformPermission = "platform:*";
}
