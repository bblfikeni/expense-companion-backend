namespace Companion.Data.Models;

/// <summary>
/// Transactional outbox row. The gateway writes atomically and its publisher relays
/// committed rows to RabbitMQ.
/// </summary>
public sealed class OutboxMessage
{
    public int Id { get; set; }

    public string Type { get; set; } = null!;

    public string Payload { get; set; } = null!;

    public string? CorrelationId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? PublishedAt { get; set; }

    public int Attempts { get; set; }

    public DateTimeOffset? NextAttemptAt { get; set; }

    /// <summary>Quarantined rows remain available for investigation and explicit replay.</summary>
    public DateTimeOffset? FailedAt { get; set; }
}
