using System;
using System.Collections.Generic;
using System.Linq;
using Companion.Extraction.Logic;

namespace Companion.Extraction.Localization;

/// <summary>
/// Classifies OCR text into document types using rule-based patterns.
/// Supports both English and Afrikaans keywords.
/// </summary>
public static class DocClassifier
{
    /// <summary>
    /// Classifies the given OCR text into a document type.
    /// </summary>
    /// <param name="ocrText">The full OCR text to classify.</param>
    /// <param name="options">The classification options with keyword lists.</param>
    /// <returns>(DocType, IsTaxInvoice, IsEftConfirmation, IsCardMachineSlip, CrossCheckReason)</returns>
    public static (string DocType, bool IsTaxInvoice, bool IsEftConfirmation, bool IsCardMachineSlip, string? CrossCheckReason) Classify(string ocrText, DocClassifierOptions options)
    {
        if (string.IsNullOrWhiteSpace(ocrText))
            return ("Unknown", false, false, false, null);

        var text = ocrText;
        var lower = text.ToLowerInvariant();

        // Check for EFT/confirmation keywords first
        foreach (var kw in options.EftKeywords)
        {
            if (!string.IsNullOrWhiteSpace(kw) && text.Contains(kw, StringComparison.OrdinalIgnoreCase))
                return ("EftConfirmation", false, true, false, $"Contains EFT keywords: {kw}");
        }

        // Check for card machine keywords
        foreach (var kw in options.CardMachineKeywords)
        {
            if (!string.IsNullOrWhiteSpace(kw) && text.Contains(kw, StringComparison.OrdinalIgnoreCase))
                return ("CardMachineSlip", false, false, true, $"Contains card machine keywords: {kw}");
        }

        // Check for Pro-forma/Quote/Estimate (non-tax indicators)
        foreach (var kw in options.NonTaxKeywords)
        {
            if (!string.IsNullOrWhiteSpace(kw) && text.Contains(kw, StringComparison.OrdinalIgnoreCase))
                return ("Unknown", false, false, false, $"Contains non-tax keyword: {kw}");
        }

        // Check for Tax Invoice keywords
        foreach (var kw in options.TaxInvoiceKeywords)
        {
            if (!string.IsNullOrWhiteSpace(kw) && text.Contains(kw, StringComparison.OrdinalIgnoreCase))
                return ("TaxInvoiceOrReceipt", TaxInvoiceDetector.Detect(text) == true, false, false, $"Contains tax invoice keyword: {kw}");
        }

        // Heuristic: if text has amount + date patterns, classify as receipt
        var hasAmount = AmountPattern(lower);
        var hasDate = DatePattern(lower);
        if (hasAmount && hasDate)
            return ("TaxInvoiceOrReceipt", false, false, false, "Has amount and date but no keyword match");

        return ("Unknown", false, false, false, null);
    }

    /// <summary>
    /// Checks if the text contains an amount pattern (R prefix or decimal number).
    /// </summary>
    private static bool AmountPattern(string text)
    {
        var regex1 = System.Text.RegularExpressions.Regex.IsMatch(text, @"r\s?[0-9]");
        var regex2 = System.Text.RegularExpressions.Regex.IsMatch(text, @"[0-9]{1,3}[.,][0-9]{2}");
        return regex1 || regex2;
    }

    /// <summary>
    /// Checks if the text contains a date pattern (dd/MM/yyyy or similar).
    /// </summary>
    private static bool DatePattern(string text)
    {
        return System.Text.RegularExpressions.Regex.IsMatch(text, @"\b\d{1,2}[/\-]\d{1,2}[/\-]\d{2,4}\b") ||
               System.Text.RegularExpressions.Regex.IsMatch(text, @"\b\d{4}-\d{2}-\d{2}\b") ||
               System.Text.RegularExpressions.Regex.IsMatch(text, @"\b\d{1,2}\s+[a-zA-Z]{3}\s+\d{4}\b");
    }
}