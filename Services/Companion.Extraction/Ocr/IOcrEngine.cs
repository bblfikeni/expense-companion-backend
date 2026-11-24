using Companion.Extraction.Ocr;

namespace Companion.Extraction.Ocr;

/// <summary>
/// Interface for OCR engines. Implementations must be self-hosted (no cloud/AI APIs).
/// </summary>
public interface IOcrEngine
{
    /// <summary>
    /// Run OCR on an image file. Returns recognized text and confidence metrics.
    /// </summary>
    /// <param name="imagePath">Path to the image file (WebP, PNG, JPEG).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>OcrResult with recognized text and confidence.</returns>
    Task<OcrResult> ExtractAsync(string imagePath, CancellationToken cancellationToken = default);
}