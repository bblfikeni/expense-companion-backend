using System.Text.RegularExpressions;

namespace Companion.Extraction.Logic;

public static class VendorExtractor
{
    public static (string? Vendor, double Confidence) Extract(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return (null, 0);
        foreach (var line in text.Split('\n').Take(5))
        {
            var candidate = line.Trim();
            if (candidate.Length < 3 || candidate.Count(char.IsLetter) < 3) continue;
            if (Regex.IsMatch(candidate, @"\b(?:tax invoice|belastingfaktuur|receipt|vat|btw|date|total|invoice|quote|estimate)\b",
                    RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)) continue;
            return (candidate, 0.6);
        }
        return (null, 0);
    }
}