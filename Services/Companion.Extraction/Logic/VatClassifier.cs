using System.Text.RegularExpressions;
using Companion.Extraction.Configuration;

namespace Companion.Extraction.Logic;

public static class VatClassifier
{
    public static (string? Classification, bool IsConsistent) Classify(
        string? text, long? totalCents, long? vatCents, ExtractionOptions options)
        => Classify(text, totalCents, vatCents, options.VatToleranceCents);
    public static (string? Classification, bool IsConsistent) Classify(
        string? text, long? totalCents, long? vatCents, long toleranceCents = 5)
    {
        if (toleranceCents < 0) throw new ArgumentOutOfRangeException(nameof(toleranceCents));
        var zero = Regex.IsMatch(text ?? "", @"\bzero[ -]rated\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        var exempt = Regex.IsMatch(text ?? "", @"\bexempt\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (totalCents is null || vatCents is null || totalCents < 0 || vatCents < 0)
            return (null, false);
        var expected = (decimal)totalCents.Value * 15 / 115;
        var consistent = vatCents.Value <= expected + toleranceCents;
        if (!consistent) return (null, false);
        if (zero || exempt)
            return (vatCents == 0 && zero != exempt ? zero ? "Zero_Rated" : "Exempt" : null, true);
        return (Math.Abs(vatCents.Value - expected) <= toleranceCents ? "Standard_15" : null, true);
    }
}