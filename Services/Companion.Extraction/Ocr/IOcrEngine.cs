namespace Companion.Extraction.Ocr;

/// <summary>Runs self-hosted OCR on an encoded image supplied in memory.</summary>
public interface IOcrEngine
{
    Task<OcrResult> ExtractAsync(byte[] imageBytes, CancellationToken cancellationToken = default);
}
