using System.Text.Json;
using Companion.Data;
using Companion.Events;
using Companion.Messaging;
using Microsoft.EntityFrameworkCore;

namespace Companion.Api.Services;

public sealed class OutboxPublisher(
    IServiceScopeFactory scopeFactory,
    ICompanionMessageBus messageBus,
    CompanionMessagingOptions messagingOptions,
    TimeProvider clock,
    ILogger<OutboxPublisher> logger) : BackgroundService
{
    private const int MaxAttempts = 10;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var pollingInterval = TimeSpan.FromMilliseconds(messagingOptions.OutboxPollingIntervalMilliseconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<CompanionDbContext>();
                var published = await PublishNextAsync(db, stoppingToken);
                if (!published)
                    await Task.Delay(pollingInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Outbox publishing failed. Retained messages will be retried or quarantined.");
                await Task.Delay(pollingInterval, stoppingToken);
            }
        }
    }

    private Task<bool> PublishNextAsync(CompanionDbContext db, CancellationToken cancellationToken)
        => db.Database.CreateExecutionStrategy()
            .ExecuteAsync(ct => PublishNextWithinTransactionAsync(db, ct), cancellationToken);

    private async Task<bool> PublishNextWithinTransactionAsync(CompanionDbContext db, CancellationToken cancellationToken)
    {
        db.ChangeTracker.Clear();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var messages = await db.OutboxMessages
            .FromSqlInterpolated($"""
                SELECT * FROM outbox_messages
                WHERE published_at IS NULL AND failed_at IS NULL
                  AND (next_attempt_at IS NULL OR next_attempt_at <= {clock.GetUtcNow()})
                ORDER BY id
                LIMIT 1
                FOR UPDATE SKIP LOCKED
                """)
            .ToListAsync(cancellationToken);
        var message = messages.FirstOrDefault();
        if (message is null)
        {
            await transaction.CommitAsync(cancellationToken);
            return false;
        }

        try
        {
            await PublishAsync(message.Type, message.Payload, cancellationToken);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            // Keep the row lock until the failure state is committed. Another
            // publisher must not publish or overwrite retry state in between.
            message.Attempts++;
            var now = clock.GetUtcNow();
            var permanentFailure = exception is JsonException or UnsupportedOutboxMessageException;
            message.FailedAt = permanentFailure || message.Attempts >= MaxAttempts ? now : null;
            message.NextAttemptAt = message.FailedAt is null
                ? now.AddSeconds(Math.Min(300, Math.Pow(2, message.Attempts - 1)))
                : null;
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            if (message.FailedAt is not null)
                logger.LogError(exception, "Quarantined outbox message {OutboxMessageId} after {Attempts} attempt(s).", message.Id, message.Attempts);
            else
                logger.LogWarning(exception, "Outbox message {OutboxMessageId} will retry at {NextAttemptAt}.", message.Id, message.NextAttemptAt);
            return true;
        }

        message.PublishedAt = clock.GetUtcNow();
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        logger.LogInformation("Published outbox message {OutboxMessageId} of type {MessageType}.", message.Id, message.Type);
        return true;
    }

    private Task PublishAsync(string messageType, string payload, CancellationToken cancellationToken)
        => messageType switch
        {
            nameof(EmailVerificationRequested) =>
                messageBus.PublishAsync(Deserialize<EmailVerificationRequested>(payload), cancellationToken),
            nameof(PasswordResetRequested) =>
                messageBus.PublishAsync(Deserialize<PasswordResetRequested>(payload), cancellationToken),
            _ => throw new UnsupportedOutboxMessageException(messageType),
        };

    private static T Deserialize<T>(string payload)
        where T : class
        => JsonSerializer.Deserialize<T>(payload, JsonOptions)
            ?? throw new JsonException($"Outbox payload for '{typeof(T).Name}' was empty.");

    private sealed class UnsupportedOutboxMessageException(string messageType)
        : Exception($"Unsupported outbox message type '{messageType}'.");
}
