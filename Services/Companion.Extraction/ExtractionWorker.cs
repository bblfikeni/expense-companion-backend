using Companion.Events;
using Companion.Messaging;
using Companion.Extraction.Configuration;
using Microsoft.Extensions.Options;

namespace Companion.Extraction;

public sealed class ExtractionWorker(
    ICompanionMessageBus bus,
    IReceiptExtractionProcessor processor,
    IOptions<ExtractionOptions> options,
    ILogger<ExtractionWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled)
        {
            logger.LogInformation("Extraction is disabled; receipt subscription was not started.");
            return;
        }

        await using var subscription = await bus.SubscribeAsync<ReceiptUploadedEvent>(
            "companion-extraction-receipt-uploaded", ProcessAsync, stoppingToken);
        logger.LogInformation("Subscribed to receipt extraction events.");
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private async Task ProcessAsync(ReceiptUploadedEvent message, CancellationToken cancellationToken)
    {
        if (!options.Value.Enabled) return;
        if (message.EventId == Guid.Empty || message.ExpensePublicId == Guid.Empty)
        {
            logger.LogWarning("Skipped invalid receipt event {EventId}.", message.EventId);
            return;
        }

        var maxAttempts = Math.Max(1, options.Value.MaxRetries);
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                logger.LogInformation("Receipt extraction started for {ExpensePublicId} ({EventId}).",
                    message.ExpensePublicId, message.EventId);
                await processor.ProcessAsync(message, cancellationToken);
                logger.LogInformation("Receipt extraction handler completed for {ExpensePublicId} ({EventId}).",
                    message.ExpensePublicId, message.EventId);
                return;
            }
            catch (Exception exception)
            {
                if (cancellationToken.IsCancellationRequested)
                    throw new OperationCanceledException(cancellationToken);
                logger.LogError("Receipt extraction failed for {ExpensePublicId} ({EventId}). Failure type: {FailureType}.",
                    message.ExpensePublicId, message.EventId, exception.GetType().Name);
                if (attempt == maxAttempts) throw;
                var seconds = Math.Clamp(options.Value.RetryBackoffBaseSeconds * Math.Pow(2, attempt - 1), 1, 300);
                await Task.Delay(TimeSpan.FromSeconds(seconds), cancellationToken);
            }
        }
    }
}