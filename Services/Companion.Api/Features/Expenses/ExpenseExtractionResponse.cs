namespace Companion.Api.Features.Expenses;

public sealed record ExpenseExtractionResponse(
    Guid ExpenseId,
    string ExpenseStatus,
    string ExtractionStatus,
    string? Vendor,
    string? ExpenseDate,
    long? TotalAmountCents,
    long? VatAmountCents,
    string? VatNumber,
    string? VatClassification,
    bool? IsValidTaxInvoice,
    double? VendorConfidence,
    double? DateConfidence,
    double? TotalConfidence,
    double? VatAmountConfidence,
    double? VatNumberConfidence,
    double? DocumentValidityConfidence,
    bool? ReviewRequired,
    DateTimeOffset? CompletedAt);