namespace Companion.Extraction.Ocr;

public interface IInMemoryOcrEngine
{
    Task<OcrResult> ExtractFromMemoryAsync(byte[] imageBytes, CancellationToken cancellationToken = default);
}