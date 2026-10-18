using Companion.Data;
using Companion.Data.Extensions;
using HealthChecks.NpgSql;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Persistence registration for the shared PostgreSQL database.
/// <para>
/// Deliberately narrow: this registers the context, its factory and a health check.
/// It does **not** register Identity and does **not** run migrations. Identity is a
/// gateway concern (<c>AddCompanionIdentity</c>); applying migrations is a startup
/// concern (<c>ApplyCompanionMigrationsAsync</c>). Workers want the database without
/// either, which is why they are kept out of here.
/// </para>
/// </summary>
public static class CompanionPostgresServiceCollectionExtensions
{
    /// <summary>Name of the health check registered by <see cref="AddCompanionPostgres"/>.</summary>
    public const string PostgresHealthCheckName = "postgres";

    public static IServiceCollection AddCompanionPostgres(
        this IServiceCollection services,
        IConfiguration configuration,
        string connectionStringName = "CompanionDb",
        Action<CompanionPostgresOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var connectionString = configuration.GetConnectionString(connectionStringName);

        // Fail at startup with an actionable message. Without this the first request
        // would surface a bare Npgsql "connection string not specified" instead.
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Connection string '{connectionStringName}' is missing or empty. " +
                $"Set it under 'ConnectionStrings:{connectionStringName}' (e.g. in appsettings.json) " +
                $"or override it with the '{connectionStringName.Replace(":", "__")}' environment variable.");
        }

        var postgresOptions = new CompanionPostgresOptions();
        configure?.Invoke(postgresOptions);

        services.AddSingleton(postgresOptions);

        // One options action shared by both registrations, so the scoped context
        // and the factory can never describe different databases or models.
        void ConfigureOptions(DbContextOptionsBuilder options)
            => options.UseCompanionDatabase(connectionString, postgresOptions);

        // Scoped context for request/unit-of-work usage (including the Identity
        // EF stores, which resolve the context from the scope)...
        services.AddDbContext<CompanionDbContext>(ConfigureOptions);

        // ...and a factory for workers and other places that own their own context
        // lifetime. Singleton (the default): it only holds options, so sharing one
        // instance is safe and intended.
        services.AddDbContextFactory<CompanionDbContext>(ConfigureOptions);

        // Connectivity only. It answers "can we reach Postgres", not "is the schema
        // current" — migration gating is a separate, later concern.
        services
            .AddHealthChecks()
            .AddNpgSql(connectionString, name: PostgresHealthCheckName);

        return services;
    }
}
