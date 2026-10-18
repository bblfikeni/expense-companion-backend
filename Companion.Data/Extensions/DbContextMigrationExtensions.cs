using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Companion.Data.Extensions;

/// <summary>
/// Migration helpers shared by every Companion service.
/// <para>
/// Only the gateway (<c>Companion.Api</c>) applies migrations, via
/// <see cref="ApplyMigrationsAsync"/>. Workers are expected to *report* pending
/// migrations using <see cref="HasPendingMigrationsAsync"/> — that check is
/// deliberately not wired anywhere yet.
/// </para>
/// </summary>
public static class DbContextMigrationExtensions
{
    /// <summary>Applies every migration in this assembly that the database is missing.</summary>
    public static async Task ApplyMigrationsAsync(
        this DbContext context,
        ILogger? logger = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var pendingMigrations = (await context.Database.GetPendingMigrationsAsync(cancellationToken))
            .ToList();

        if (pendingMigrations.Count == 0)
        {
            logger?.LogInformation("Database schema is already up to date.");
            return;
        }

        logger?.LogInformation(
            "Applying {PendingMigrationCount} pending migration(s): {PendingMigrations}",
            pendingMigrations.Count,
            string.Join(", ", pendingMigrations));

        await context.Database.MigrateAsync(cancellationToken);

        logger?.LogInformation("Database schema migrated.");
    }

    /// <summary>Returns true when the database is missing one or more migrations.</summary>
    public static async Task<bool> HasPendingMigrationsAsync(
        this DbContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var pendingMigrations = await context.Database.GetPendingMigrationsAsync(cancellationToken);

        return pendingMigrations.Any();
    }
}
