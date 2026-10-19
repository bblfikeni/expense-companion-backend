namespace Companion.Data.Models;

/// <summary>
/// Transactional outbox row. The gateway owns the write side and never consumes from
/// RabbitMQ: relay is a later slice.
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
}
