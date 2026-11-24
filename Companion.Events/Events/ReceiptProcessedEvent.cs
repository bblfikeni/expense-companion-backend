namespace Companion.Events;

/// <summary>
/// Fired when OCR extraction of a receipt is complete.
/// Carries identifiers and status only - NO image data, NO extracted field values,
/// NO int IDs (per the identifiers-only rule). The client refetches data from the gateway.
/// </summary>
public sealed class ReceiptProcessedEvent
{
    /// <summary>
    /// Stable public identifier of the expense.
    /// </summary>
    public Guid ExpensePublicId { get; init; }

    /// <summary>
    /// Final extraction status.
    /// </summary>
    public string Status { get; init; } = null!; // Qualified / Rejected

    /// <summary>
    /// true if the result requires user review before final acceptance.
    /// </summary>
    public bool ReviewRequired { get; init; }

    /// <summary>
    /// Correlation ID for end-to-end tracing across HTTP/RabbitMQ.
    /// </summary>
    public string? CorrelationId { get; init; }

    /// <summary>
    /// Unique event ID for deduplication and tracking.
    /// </summary>
    public Guid EventId { get; init; }

    /// <summary>
    /// When this event occurred (UTC).
    /// </summary>
    public DateTimeOffset OccurredAtUtc { get; init; }
}