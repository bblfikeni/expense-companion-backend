using System.Globalization;
using System.Text.RegularExpressions;

namespace Companion.Extraction.Logic;

public static class AmountParser
{
    public static long? Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var text = value.Trim();
        if (text.StartsWith('R') || text.StartsWith('r')) text = text[1..].Trim();
        if (!Regex.IsMatch(text, @"^[0-9][0-9 ,.]*$", RegexOptions.CultureInvariant)) return null;

        var lastSeparator = Math.Max(text.LastIndexOf('.'), text.LastIndexOf(','));
        var hasDecimals = lastSeparator >= 0 && text.Length - lastSeparator - 1 == 2;
        var whole = hasDecimals ? text[..lastSeparator] : text;
        var fraction = hasDecimals ? text[(lastSeparator + 1)..] : "00";
        if (!Regex.IsMatch(whole, @"^(?:[0-9]+|[0-9]{1,3}(?:[ ,.][0-9]{3})+)$", RegexOptions.CultureInvariant)
            || !Regex.IsMatch(fraction, @"^[0-9]{2}$", RegexOptions.CultureInvariant)) return null;
        var digits = whole.Replace(" ", "").Replace(",", "").Replace(".", "") + fraction;
        return long.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var cents) ? cents : null;
    }
}