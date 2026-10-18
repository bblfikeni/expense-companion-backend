using Microsoft.EntityFrameworkCore;

namespace Companion.Data.Extensions;

/// <summary>
/// Single place where the shared PostgreSQL database is wired up, so that every
/// service which registers <see cref="CompanionDbContext"/> gets an identical
/// model. All services share one flat schema, so a service that forgot the
/// snake_case convention would silently produce a divergent model.
/// <para>
/// EFCore.NamingConventions 9.0+ removed the <c>ModelBuilder</c> overload, which
/// is why this lives at the options level rather than inside the DbContext.
/// </para>
/// </summary>
public static class DbContextOptionsBuilderExtensions
{
    public static DbContextOptionsBuilder UseCompanionDatabase(
        this DbContextOptionsBuilder optionsBuilder,
        string connectionString)
        => optionsBuilder.UseCompanionDatabase(connectionString, new CompanionPostgresOptions());

    public static DbContextOptionsBuilder UseCompanionDatabase(
        this DbContextOptionsBuilder optionsBuilder,
        string connectionString,
        CompanionPostgresOptions postgresOptions)
    {
        ArgumentException.ThrowIfNullOrEmpty(connectionString);
        ArgumentNullException.ThrowIfNull(postgresOptions);

        optionsBuilder.UseNpgsql(
            connectionString,
            npgsql =>
            {
                npgsql.CommandTimeout((int)postgresOptions.CommandTimeout.TotalSeconds);

                if (postgresOptions.EnableRetryOnFailure)
                {
                    // EF's own execution strategy. Enabling it here rather than via a
                    // wrapper means a caller that opens a transaction automatically gets
                    // the retrying strategy through CreateExecutionStrategy().
                    // The third argument is an optional extra set of transient error codes.
                    npgsql.EnableRetryOnFailure(
                        postgresOptions.MaxRetryCount,
                        postgresOptions.MaxRetryDelay,
                        null);
                }
            });

        optionsBuilder.UseSnakeCaseNamingConvention();

        if (postgresOptions.EnableSensitiveDataLogging)
        {
            optionsBuilder.EnableSensitiveDataLogging();
        }

        if (postgresOptions.EnableDetailedErrors)
        {
            optionsBuilder.EnableDetailedErrors();
        }

        return optionsBuilder;
    }
}
