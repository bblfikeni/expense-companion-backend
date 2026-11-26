using System.Text.Json.Serialization;

namespace Companion.Data.Models;

/// <summary>
/// One row per extraction run. Persists the OCR result so the gateway can
/// read it back and the ReceiptProcessedEvent can carry IDs and status only.
/// </summary>
public sealed class ExpenseExtraction
{
    /// <summary>Primary key.</summary>
    public int Id { get; set; }

    /// <summary>FK to profiles.id (the expense owner).</summary>
    public int ExpenseId { get; set; }

    /// <summary>Public identifier of the expense (Guid, never the int PK).</summary>
    public Guid ExpensePublicId { get; set; }

    /// <summary>Extraction status.</summary>
    public string Status { get; set; } = null!; // None/Processing/Qualified/Rejected

    /// <summary>Overall confidence 0.00 to 1.00.</summary>
    public double OverallConfidence { get; set; }

    /// <summary>Hash of the image set for idempotency checks.</summary>
    public string ImageSetHash { get; set; } = null!;

    /// <summary> true if the result requires user review before final acceptance.</summary>
    public bool ReviewRequired { get; set; }

    /// <summary>Reason code if status is Rejected.</summary>
    public string? RejectionReason { get; set; }

    /// <summary>Vendor name extracted from the receipt.</summary>
    public string? Vendor { get; set; }

    /// <summary>Transaction date extracted from the receipt.</summary>
    public string? ExpenseDate { get; set; }

    /// <summary>Total amount in cents (extracted value).</summary>
    public long? TotalAmountCents { get; set; }

    /// <summary>VAT amount in cents (extracted value).</summary>
    public long? VatAmountCents { get; set; }

    /// <summary>VAT registration number extracted from the receipt.</summary>
    public string? VatNumber { get; set; }

    /// <summary>Confidence in the vendor field.</summary>
    public double? VendorConfidence { get; set; }

    /// <summary>Confidence in the date field.</summary>
    public double? DateConfidence { get; set; }

    /// <summary>Confidence in the total field.</summary>
    public double? TotalConfidence { get; set; }

    /// <summary>Confidence in the VAT amount field.</summary>
    public double? VatAmountConfidence { get; set; }

    /// <summary>Confidence in the VAT number field.</summary>
    public double? VatNumberConfidence { get; set; }

    /// <summary>Confidence in the document validity field.</summary>
    public double? DocumentValidityConfidence { get; set; }

    /// <summary>VAT classification.</summary>
    public string? VatClassification { get; set; } // Standard_15 / Zero_Rated / Exempt / Not_Claimable / Second_Hand / null

    /// <summary> true if the receipt is a valid tax invoice.</summary>
    public bool? IsValidTaxInvoice { get; set; }

    /// <summary>When extraction started (UTC).</summary>
    public DateTimeOffset StartedAt { get; set; }

    /// <summary>When extraction completed (UTC).</summary>
    public DateTimeOffset? CompletedAt { get; set; }
}