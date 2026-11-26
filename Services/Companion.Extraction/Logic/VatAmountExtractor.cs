using System.Text.RegularExpressions;

namespace Companion.Extraction.Logic;

public static class VatAmountExtractor
{
    public static long? Extract(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        foreach (var line in text.Split('\n'))
        {
            var match = Regex.Match(line, @"^\s*(?:VAT|BTW)\b\s*(?:amount|bedrag)?\s*[:=]?\s*(R?\s*\d[\d ,.]*\d|R?\s*\d)\s*$",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (match.Success && AmountParser.Parse(match.Groups[1].Value.Trim()) is { } amount) return amount;
        }
        return null;
    }
}