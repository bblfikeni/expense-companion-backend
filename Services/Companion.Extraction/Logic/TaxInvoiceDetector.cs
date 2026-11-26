using System.Text.RegularExpressions;

namespace Companion.Extraction.Logic;

public static class TaxInvoiceDetector
{
    public static bool? Detect(string? text)
    {
        text ??= string.Empty;
        const RegexOptions options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;
        if (Regex.IsMatch(text, @"\b(?:pro[ -]?forma|quote|estimate)\b", options)) return false;
        return Regex.IsMatch(text, @"\b(?:tax\s+invoice|belastingfaktuur)\b", options) ? true : null;
    }
}