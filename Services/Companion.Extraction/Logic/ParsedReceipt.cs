namespace Companion.Extraction.Logic;

public sealed record ParsedReceipt(
    string? Vendor, DateOnly? Date, long? TotalCents, long? VatAmountCents,
    string? VatNumber, string? VatClassification, bool? IsValidTaxInvoice,
    double VendorConfidence, double DateConfidence, double TotalConfidence,
    double VatAmountConfidence, double VatNumberConfidence, double DocumentValidityConfidence);