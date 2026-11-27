using Companion.Data;
using Companion.Events;
using Companion.Extraction.Configuration;
using Companion.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Companion.Extraction;

public sealed class ReceiptRecoveryWorker(
    IDbContextFactory<CompanionDbContext> contextFactory,
    ReceiptExtractionProcessor processor,
    ICompanionMessageBus bus,
    TimeProvider clock,
    IOptions<ExtractionOptions> options,
    ILogger<ReceiptRecoveryWorker> logger) : BackgroundService
{
    private const int PageSize = 100;
    private static readonly TimeSpan SweepInterval = TimeSpan.FromSeconds(30);
    private readonly SemaphoreSlim _sweepLock = new(1, 1);
    private int _cursor;
    private int? _roundEnd;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled) return;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SweepOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogError("Receipt recovery sweep failed; failure type {FailureType}.", exception.GetType().Name);
            }
            await Task.Delay(SweepInterval, stoppingToken);
        }
    }

    public async Task SweepOnceAsync(CancellationToken cancellationToken)
    {
        if (!options.Value.Enabled) return;
        await _sweepLock.WaitAsync(cancellationToken);
        try
        {
            await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
            var now = clock.GetUtcNow();
            var expiredBefore = now - ExtractionOptions.ProcessingLease;
            var candidates = db.Expenses.FromSqlInterpolated($"""
                SELECT e.* FROM expenses e
                WHERE e.status = 'Processing' AND EXISTS (
                    SELECT 1 FROM public.published_receipt_events o
                    WHERE o.expense_public_id = e.public_id::text
                      AND (o.published_at <= {expiredBefore} OR EXISTS (
                          SELECT 1 FROM expense_extractions x WHERE x.expense_id = e.id
                      ))
                ) AND NOT EXISTS (
                    SELECT 1 FROM expense_extractions x
                    WHERE x.expense_id = e.id
                      AND (x.completed_at IS NOT NULL OR x.status NOT IN ('Processing', 'RetryPending')
                           OR (x.status = 'Processing' AND x.started_at > {expiredBefore})
                           OR (x.status = 'RetryPending' AND x.next_attempt_at > {now}))
                )
                """).AsNoTracking();
            _roundEnd ??= await candidates
                .MaxAsync(e => (int?)e.Id, cancellationToken);
            if (_roundEnd is null) return;
            var expenses = await candidates
                .Where(e => e.Id > _cursor && e.Id <= _roundEnd.Value)
                .OrderBy(e => e.Id)
                .Select(e => new { e.Id, e.PublicId })
                .Take(PageSize)
                .ToListAsync(cancellationToken);
            foreach (var expense in expenses)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    if (await processor.CanRepublishAsync(expense.PublicId, cancellationToken))
                    {
                        await bus.PublishAsync(new ReceiptUploadedEvent
                        {
                            EventId = Guid.NewGuid(),
                            ExpensePublicId = expense.PublicId,
                            RequestedAt = now,
                            OccurredAtUtc = now,
                        }, cancellationToken);
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    logger.LogError("Receipt recovery could not process {ExpensePublicId}; failure type {FailureType}.",
                        expense.PublicId, exception.GetType().Name);
                }
                _cursor = expense.Id;
            }
            if (expenses.Count < PageSize || _cursor == _roundEnd)
            {
                _cursor = 0;
                _roundEnd = null;
            }
        }
        finally
        {
            _sweepLock.Release();
        }
    }
}
