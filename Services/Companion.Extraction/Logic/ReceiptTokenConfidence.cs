using Companion.Extraction.Ocr;

namespace Companion.Extraction.Logic;

public static class ReceiptTokenConfidence
{
    public static double For(string? evidence, IReadOnlyCollection<OcrResult.OcrWordResult> tokens)
    {
        if (string.IsNullOrWhiteSpace(evidence)) return 0;
        var words = evidence.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        var matches = tokens.Where(token => !string.IsNullOrWhiteSpace(token.Text)
            && words.Any(word => string.Equals(word.Trim(':', '=', '.'), token.Text.Trim(':', '=', '.'),
                StringComparison.OrdinalIgnoreCase))).ToArray();
        return matches.Length == 0 ? 0 : matches.Average(token => Math.Clamp(token.Confidence, 0, 1));
    }
}