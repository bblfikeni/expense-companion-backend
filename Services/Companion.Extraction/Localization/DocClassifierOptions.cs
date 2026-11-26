namespace Companion.Extraction.Localization;

/// <summary>
/// Configurable rules for document type classification.
/// All patterns are case-insensitive and applied to OCR text.
/// </summary>
public class DocClassifierOptions
{
    /// <summary>Keywords for Tax Invoice (English + Afrikaans).</summary>
    public HashSet<string> TaxInvoiceKeywords { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        "Tax Invoice", "Belastingfaktuur", "fiscal invoice", "official receipt", "receipt tax"
    };

    /// <summary>Keywords for Pro-forma/Quote/Estimate.</summary>
    public HashSet<string> NonTaxKeywords { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        "Pro-forma", "Proforma", "Quote", "Estimate", "Quotation"
    };

    /// <summary>Keywords for EFT/confirmation.</summary>
    public HashSet<string> EftKeywords { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        "EFT", "electronic fund", "bank transfer", "telegraphic transfer", "wire transfer", "payment advice"
    };

    /// <summary>Keywords for card machine slips.</summary>
    public HashSet<string> CardMachineKeywords { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        "card machine", "terminal", "POS", "approved", "auth code", "settlement"
    };

    /// <summary>English month names.</summary>
    public HashSet<string> EnglishMonthNames { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        "January", "February", "March", "April", "May", "June", "July", "August", "September", "October", "November", "December"
    };

    /// <summary>Afrikaans month names.</summary>
    public HashSet<string> AfrikaansMonthNames { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        "Januarie", "Februarie", "Maart", "April", "Mei", "Junie", "Julie", "Augustus", "September", "Oktober", "November", "Desember"
    };
}