using Companion.Data;
using Companion.Data.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;

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

        services.AddDataProtection();

        // Storage + UserManager/RoleManager only. Deliberately no authentication scheme:
        // AddIdentity() would register cookie schemes and make them the default
        // authenticate/forbid schemes for the whole gateway before the auth phase has
        // decided what it wants. Cookies and JWT both belong to that later phase.
        services
            .AddIdentityCore<Profile>(options => options.User.RequireUniqueEmail = true)
            .AddRoles<Role>()
            .AddEntityFrameworkStores<CompanionDbContext>()
            .AddDefaultTokenProviders();

        // SignInManager is not registered by AddIdentityCore; register it explicitly.
        services.AddHttpContextAccessor();
        services.AddScoped<SignInManager<Profile>>();

        return services;
    }
}
