using System;
using System.Globalization;

using Tesseract;

namespace Companion.Extraction.Ocr;

/// <summary>
/// Placeholder OCR engine returning empty results.
/// To be replaced with a real implementation (e.g. Tesseract) when the
/// native libraries are properly configured.
/// </summary>
public sealed class TesseractOcrEngine : IOcrEngine
{
    public static string DataPath { get; set; } = "/usr/share/tessdata";

    private readonly string _languages;

    public TesseractOcrEngine(string? languages = null)
    {
        _languages = languages ?? "eng,afr";
    }

    public async Task<OcrResult> ExtractAsync(string imagePath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // TODO: Implement real Tesseract OCR when native libraries are configured.
        // For now return empty result so the pipeline can be tested.
        await Task.Yield();

        return new OcrResult
        {
            Text = string.Empty,
            MeanConfidence = 0.0
        };
    }
}