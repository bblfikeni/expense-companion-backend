using System;
using System.Globalization;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
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
    private readonly ILogger<TesseractOcrEngine> _logger;

    public TesseractOcrEngine(string? languages = null, ILogger<TesseractOcrEngine>? logger = null)
    {
        _languages = languages ?? "eng,afr";
        _logger = logger ?? NullLogger<TesseractOcrEngine>.Instance;
    }

    public async Task<OcrResult> ExtractAsync(string imagePath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _logger.LogInformation("OCR extraction started.");

        // TODO: Implement real Tesseract OCR when native libraries are configured.
        // For now return empty result so the pipeline can be tested.
        await Task.Yield();
        _logger.LogWarning("OCR extraction is not implemented; returning an empty result.");

        return new OcrResult
        {
            Text = string.Empty,
            MeanConfidence = 0.0
        };
    }
}