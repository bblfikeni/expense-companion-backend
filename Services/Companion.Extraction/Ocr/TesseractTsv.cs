using System.Drawing;
using System.Globalization;

namespace Companion.Extraction.Ocr;

internal static class TesseractTsv
{
    internal static OcrResult Parse(string tsv, CancellationToken cancellationToken)
    {
        var pages = new Dictionary<int, Size>();
        var lines = new Dictionary<(int Page, int Block, int Paragraph, int Line), OcrResult.OcrLineResult>();
        using var reader = new StringReader(tsv);
        while (reader.ReadLine() is { } row)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var fields = row.Split('\t', 12);
            if (fields.Length != 12 || !TryInteger(fields[1], out var page) ||
                !TryInteger(fields[8], out var width) || !TryInteger(fields[9], out var height))
                continue;

            if (fields[0] == "1" && width > 0 && height > 0)
                pages[page] = new Size(width, height);

            if (fields[0] != "5" || !pages.TryGetValue(page, out var size) ||
                !TryInteger(fields[2], out var block) || !TryInteger(fields[3], out var paragraph) ||
                !TryInteger(fields[4], out var lineNumber) || !TryInteger(fields[6], out var left) ||
                !TryInteger(fields[7], out var top) ||
                !double.TryParse(fields[10], NumberStyles.Float, CultureInfo.InvariantCulture, out var confidence) ||
                !double.IsFinite(confidence) || confidence < 0 || string.IsNullOrWhiteSpace(fields[11]))
                continue;

            var key = (page, block, paragraph, lineNumber);
            if (!lines.TryGetValue(key, out var line))
            {
                line = new OcrResult.OcrLineResult { PageNo = page };
                lines.Add(key, line);
            }

            var x = Math.Clamp(left / (float)size.Width, 0, 1);
            var y = Math.Clamp(top / (float)size.Height, 0, 1);
            var right = Math.Clamp(((float)left + width) / size.Width, x, 1);
            var bottom = Math.Clamp(((float)top + height) / size.Height, y, 1);
            line.Words.Add(new OcrResult.OcrWordResult
            {
                Text = fields[11].Trim(),
                Confidence = Math.Clamp(confidence / 100, 0, 1),
                BoundingBox = RectangleF.FromLTRB(x, y, right, bottom),
            });
        }

        foreach (var line in lines.Values)
        {
            line.Text = string.Join(' ', line.Words.Select(word => word.Text));
            line.Confidence = line.Words.Average(word => word.Confidence);
        }

        return new OcrResult
        {
            Text = string.Join('\n', lines.Values.Select(line => line.Text)),
            MeanConfidence = lines.Count == 0 ? 0 : lines.Values.SelectMany(line => line.Words).Average(word => word.Confidence),
            Lines = lines.Values.ToArray(),
        };
    }

    private static bool TryInteger(string value, out int number) =>
        int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out number);
}
