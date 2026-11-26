using System.Text.RegularExpressions;

namespace Companion.Extraction.Logic;

public static class ReceiptFieldSelector
{
    private const RegexOptions Options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

    public static string? Total(string text) => Match(text,
        @"(?im)^\s*(?:grand\s+total|total|totaal)\s*[:=]?\s*(R?\s*\d[\d ,.]*\d|R?\s*\d)\s*$");

    public static string? Date(string text) => Match(text,
        @"\b(\d{2}/\d{2}/\d{4}|\d{4}-\d{2}-\d{2}|\d{2}\s+[A-Za-z]{3}\s+\d{4})\b");

    public static string? VatNumber(string text) => Match(text,
        @"(?im)^\s*(?:vat|btw)\s*(?:number|no\.?|nr\.?|nommer)\s*[:=]?\s*(\d+)\s*$");

    public static string? VatAmount(string text) => Match(text,
        @"(?im)^\s*(?:vat|btw)\b\s*(?:amount|bedrag)?\s*[:=]?\s*(R?\s*\d[\d ,.]*\d|R?\s*\d)\s*$");

    public static string? DocumentValidity(string text) => Match(text,
        @"\b(pro[ -]?forma|quote|estimate)\b") ?? Match(text, @"\b(tax\s+invoice|belastingfaktuur)\b");

    private static string? Match(string text, string pattern)
    {
        var match = Regex.Match(text, pattern, Options);
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }
}