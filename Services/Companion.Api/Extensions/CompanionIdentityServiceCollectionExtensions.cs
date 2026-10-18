using Companion.Data;
using Companion.Data.Models;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// ASP.NET Core Identity wiring for the gateway.
/// <para>
/// This lives in Companion.Api, not Companion.Data, on purpose: the workers share the
/// database schema but have no users to authenticate, so they should not pay for
/// Identity services. Companion.Data therefore registers persistence only.
/// </para>
/// </summary>
public static class CompanionIdentityServiceCollectionExtensions
{
    public static IServiceCollection AddCompanionIdentity(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Storage + UserManager/RoleManager only. Deliberately no authentication scheme:
        // AddIdentity() would register cookie schemes and make them the default
        // authenticate/forbid schemes for the whole gateway before the auth phase has
        // decided what it wants. Cookies and JWT both belong to that later phase.
        services
            .AddIdentityCore<Profile>(options => options.User.RequireUniqueEmail = true)
            .AddRoles<Role>()
            .AddEntityFrameworkStores<CompanionDbContext>();

        return services;
    }
}
