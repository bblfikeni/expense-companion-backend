using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

using Companion.Data.Extensions;

namespace Companion.Data;

/// <summary>
/// Design-time factory so <c>dotnet ef</c> can build <see cref="CompanionDbContext"/>
/// without a running host. It mirrors production
/// <see cref="CompanionPostgresServiceCollectionExtensions.AddCompanionPostgres"/>
/// wiring (shared options action, snake_case).
/// </summary>
internal sealed class CompanionDesignTimeDbContextFactory : IDesignTimeDbContextFactory<CompanionDbContext>
{
    public CompanionDbContext CreateDbContext(string[] args)
    {
        var connectionString = ResolveConnectionString(args);
        ArgumentException.ThrowIfNullOrEmpty(connectionString);

        var optionsBuilder = new DbContextOptionsBuilder<CompanionDbContext>();
        ((DbContextOptionsBuilder)optionsBuilder).UseCompanionDatabase(connectionString, new CompanionPostgresOptions());

        return new CompanionDbContext(optionsBuilder.Options);
    }

    private static string ResolveConnectionString(string[] args)
    {
        // dotnet ef ... --connection "Host=..."
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] == "--connection" && i + 1 < args.Length)
            {
                return args[i + 1];
            }
        }

        // CompanionDb from environment (ConnectionStrings__CompanionDb -> ConnectionStrings:CompanionDb).
        var fromEnvironment = Environment.GetEnvironmentVariable("ConnectionStrings__CompanionDb");
        if (!string.IsNullOrWhiteSpace(fromEnvironment))
        {
            return fromEnvironment;
        }

        throw new InvalidOperationException("No CompanionDb connection string configured for design-time EF.");
    }
}


