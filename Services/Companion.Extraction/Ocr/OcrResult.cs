using System.Drawing;

namespace Companion.Extraction.Ocr;

/// <summary>
/// Result from running OCR on a single image.
/// </summary>
public sealed class OcrResult
{
    /// <summary>The full recognized text.</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>Mean confidence across all tokens (0.0 to 1.0).</summary>
    public double MeanConfidence { get; set; }

    /// <summary>Per-line results with bounding boxes.</summary>
    public ICollection<OcrLineResult> Lines { get; set; } = new List<OcrLineResult>();

    /// <summary>Whether the OCR engine found usable text above a minimum threshold.</summary>
    public bool HasValidText => MeanConfidence >= 0.5 && Lines.Any(l => l.Words.Any(w => w.Confidence >= 0.5));
}

/// <summary>
/// Per-line OCR result with words and confidence.
/// </summary>
public sealed class OcrLineResult
{
    /// <summary>Line text.</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>Mean confidence for this line.</summary>
    public double Confidence { get; set; }

    /// <summary>Words in this line with bounding boxes.</summary>
    public ICollection<OcrWordResult> Words { get; set; } = new List<OcrWordResult>();
}

/// <summary>
/// Per-word OCR result with bounding box and confidence.
/// </summary>
public sealed class OcrWordResult
{
    /// <summary>Word text.</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>Confidence for this word (0.0 to 1.0).</summary>
    public double Confidence { get; set; }

    /// <summary>Bounding box (normalized coordinates).</summary>
    public RectangleF BoundingBox { get; set; }
}