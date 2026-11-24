namespace Companion.Events;

/// <summary>
/// Fired when a receipt capture process is initialized.
/// Carries identifiers only - NO image data, NO int IDs (per the identifiers-only rule).
/// </summary>
public sealed class ReceiptUploadedEvent
{
    /// <summary>
    /// Stable public identifier of the expense.
    /// </summary>
    public Guid ExpensePublicId { get; init; }

    /// <summary>
    /// Correlation ID for end-to-end tracing across HTTP/RabbitMQ.
    /// </summary>
    public string? CorrelationId { get; init; }

    /// <summary>
    /// When the receipt upload was originally requested (UTC).
    /// </summary>
    public DateTimeOffset RequestedAt { get; init; }

    /// <summary>
    /// Unique event ID for deduplication and tracking.
    /// </summary>
    public Guid EventId { get; init; }

    /// <summary>
    /// When this event occurred (UTC).
    /// </summary>
    public DateTimeOffset OccurredAtUtc { get; init; }
}