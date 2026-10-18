using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Companion.Data.Extensions;

/// <summary>
/// Migration entry points, expressed against <see cref="IHost"/> so a caller cannot
/// accidentally apply migrations without a service provider.
/// <para>
/// Only the gateway (<c>Companion.Api</c>) calls
/// <see cref="ApplyCompanionMigrationsAsync"/>. Workers use
/// <see cref="EnsureCompanionMigrationsAppliedAsync"/> to *verify* the schema rather
/// than change it — that check is intentionally not wired anywhere yet.
/// </para>
/// </summary>
public static class CompanionMigrationHostExtensions
{
    /// <summary>Applies every migration the database is missing. Gateway only.</summary>
    public static async Task ApplyCompanionMigrationsAsync(
        this IHost host,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(host);

        var logger = CreateLogger(host);

        await using var scope = host.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CompanionDbContext>();

        var pendingMigrations = (await dbContext.Database.GetPendingMigrationsAsync(cancellationToken))
            .ToList();

        if (pendingMigrations.Count == 0)
        {
            logger.LogInformation("Database schema is already up to date.");
            return;
        }

        logger.LogInformation(
            "Applying {PendingMigrationCount} pending migration(s): {PendingMigrations}",
            pendingMigrations.Count,
            string.Join(", ", pendingMigrations));

        await dbContext.Database.MigrateAsync(cancellationToken);

        logger.LogInformation("Database schema migrated.");
    }

    /// <summary>
    /// Verifies the schema is already current, retrying with exponential backoff so a
    /// host that started while Postgres was still booting does not die immediately.
    /// Throws — and therefore fails startup — if migrations are still missing once the
    /// attempts are exhausted. It never applies anything itself.
    /// </summary>
    public static async Task EnsureCompanionMigrationsAppliedAsync(
        this IHost host,
        int maxAttempts = 5,
        TimeSpan? baseDelay = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(host);

        if (maxAttempts < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxAttempts), maxAttempts, "At least one attempt is required.");
        }

        var logger = CreateLogger(host);
        var backoffStep = baseDelay ?? TimeSpan.FromSeconds(1);

        List<string>? pendingMigrations = null;
        Exception? lastFailure = null;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                await using var scope = host.Services.CreateAsyncScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<CompanionDbContext>();

                pendingMigrations = (await dbContext.Database.GetPendingMigrationsAsync(cancellationToken))
                    .ToList();
                lastFailure = null;

                if (pendingMigrations.Count == 0)
                {
                    logger.LogInformation(
                        "Database schema confirmed up to date on attempt {Attempt}/{MaxAttempts}.",
                        attempt,
                        maxAttempts);
                    return;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Treat "cannot reach the database yet" the same as "not applied yet":
                // both are usually just Postgres still starting.
                pendingMigrations = null;
                lastFailure = ex;
            }

            if (attempt == maxAttempts)
            {
                break;
            }

            var delay = TimeSpan.FromTicks(backoffStep.Ticks * (1L << (attempt - 1)));

            logger.LogWarning(
                "Migration check {Attempt}/{MaxAttempts} could not confirm the schema ({Reason}); retrying in {Backoff}.",
                attempt,
                maxAttempts,
                lastFailure?.Message ?? $"{pendingMigrations!.Count} migration(s) still pending",
                delay);

            await Task.Delay(delay, cancellationToken);
        }

        if (lastFailure is not null)
        {
            throw new InvalidOperationException(
                $"Could not confirm the database schema after {maxAttempts} attempt(s); " +
                "the last check failed. Refusing to start rather than serve traffic against an unknown schema.",
                lastFailure);
        }

        throw new InvalidOperationException(
            $"Database is still missing {pendingMigrations!.Count} migration(s) after {maxAttempts} attempt(s): " +
            $"{string.Join(", ", pendingMigrations)}. This host requires the schema to be current before it starts. " +
            "Run the gateway (Companion.Api), which owns migrations, or apply them out of band.");
    }

    private static ILogger CreateLogger(IHost host)
        => host.Services
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger(typeof(CompanionMigrationHostExtensions));
}
