using Companion.Events;

namespace Companion.Extraction;

public interface IReceiptExtractionProcessor
{
    Task ProcessAsync(ReceiptUploadedEvent message, CancellationToken cancellationToken);
}