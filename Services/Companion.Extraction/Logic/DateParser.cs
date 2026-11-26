using System.Globalization;
using System.Text.RegularExpressions;

namespace Companion.Extraction.Logic;

public static class DateParser
{
    public static DateOnly? Parse(string? value, DateOnly today)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var text = Regex.Replace(value.Trim(), @"\b(Mrt|Mei|Okt|Des)\b", match =>
            match.Value.ToLowerInvariant() switch
            {
                "mrt" => "Mar", "mei" => "May", "okt" => "Oct", _ => "Dec"
            }, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        string[] formats = ["dd/MM/yyyy", "yyyy-MM-dd", "dd MMM yyyy"];
        if (!DateOnly.TryParseExact(text, formats, CultureInfo.GetCultureInfo("en-US"),
                DateTimeStyles.None, out var date)) return null;
        return date > today || date < today.AddYears(-5) ? null : date;
    }
}