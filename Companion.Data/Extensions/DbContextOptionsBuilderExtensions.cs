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
    {
        ArgumentException.ThrowIfNullOrEmpty(connectionString);

        return optionsBuilder
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention();
    }
}
