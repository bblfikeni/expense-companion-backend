using Companion.Data;
using Companion.Data.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Companion.Api.Tests.Infrastructure;

/// <summary>
/// Builds throwaway hosts that mirror how real services register persistence.
/// <see cref="withIdentity"/> is false by default because the workers register
/// persistence only — that is the case the "no Identity in a non-gateway host" test
/// pins down.
/// </summary>
internal static class CompanionHosts
{
    public static IHost Build(
        string connectionString,
        bool withIdentity = false,
        Action<CompanionPostgresOptions>? configure = null)
    {
        var builder = Host.CreateApplicationBuilder();

        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:CompanionDb"] = connectionString,
        });

        builder.Services.AddCompanionPostgres(builder.Configuration, configure: configure);

        if (withIdentity)
        {
            builder.Services.AddCompanionIdentity();
        }

        return builder.Build();
    }
}
