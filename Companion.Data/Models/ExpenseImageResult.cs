using System.Text.Json.Serialization;

namespace Companion.Data.Models;

/// <summary>
/// One row per image within an extraction run. Stores per-image OCR results
/// and quality scores.
/// </summary>
public sealed class ExpenseImageResult
{
    /// <summary>Primary key.</summary>
    public int Id { get; set; }

    /// <summary>FK to expense_extractions.id.</summary>
    public int ExtractionId { get; set; }

    /// <summary>Identifier for the image within the expense (page_no).</summary>
    public int PageNo { get; set; }

    /// <summary>Detected document type.</summary>
    public string DocumentType { get; set; } = null!; // TaxInvoiceOrReceipt / EftConfirmation / CardMachineSlip / Unknown

    /// <summary>OCR engine used (e.g. Tesseract, Tailor).</summary>
    public string OcrEngine { get; set; } = null!;

    /// <summary>Per-image quality score (blur/contrast/resolution heuristic).</summary>
    public double ImageQualityScore { get; set; }

    /// <summary>Mean OCR confidence across all tokens in this image.</summary>
    public double OcrMeanConfidence { get; set; }

    /// <summary>Extracted proof-of-payment amount in cents (if applicable).</summary>
    public long? ProofAmountCents { get; set; }

    /// <summary>Extracted proof-of-payment date (if applicable).</summary>
    public string? ProofDate { get; set; }

    /// <summary>Reason if this image failed processing.</summary>
    public string? FailureReason { get; set; }

    /// <summary>Navigation property.</summary>
    public ExpenseExtraction? Extraction { get; set; }
}