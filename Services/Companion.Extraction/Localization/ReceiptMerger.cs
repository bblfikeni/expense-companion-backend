using Companion.Extraction.Ocr;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Companion.Extraction.Localization;

/// <summary>
/// Merges OCR line texts from multiple receipt-type images in page_no order into one consolidated text.
/// Duplicate adjacent lines (tolerant to minor OCR differences) are removed.
/// Proof-of-payment images are never merged into receipt text.
/// </summary>
public static class ReceiptMerger
{
    /// <summary>
    /// Merges OCR line texts from multiple images in page_no order into one consolidated text.
    /// Duplicate adjacent lines (tolerant to minor OCR differences) are removed.
    /// </summary>
    /// <param name="ocrLines">OCR line results with text and page_no, only for receipt-type images.</param>
    /// <returns>Merged text with lines in page_no order, duplicates removed.</returns>
    public static string MergeI(List<OcrResult.OcrLineResult> ocrLines)
    {
        if (ocrLines == null || ocrLines.Count == 0)
            return string.Empty;

        // OrderBy preserves the input line order within each page.
        var sorted = ocrLines
            .OrderBy(l => l.PageNo)
            .ToList();

        var merged = new List<string>();
        var lastLine = string.Empty;

        foreach (var line in sorted)
        {
            var normalized = NormalizeLine(line.Text);
            var lastNormalized = NormalizeLine(lastLine);

            // Skip if this is a duplicate of the last line (tolerant to minor OCR differences)
            if (normalized == lastNormalized)
                continue;

            merged.Add(line.Text);
            lastLine = line.Text;
        }

        return string.Join("\n", merged);
    }

    /// <summary>
    /// Normalizes a line for comparison: trim, lowercase, remove extra spaces.
    /// </summary>
    private static string NormalizeLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return string.Empty;

        var trimmed = line.Trim().ToLowerInvariant();
        // Collapse multiple spaces
        var collapsed = System.Text.RegularExpressions.Regex.Replace(trimmed, @"\s+", " ");
        return collapsed;
    }

    /// <summary>
    /// Determines whether a document type is a receipt/invoice type that should be merged.
    /// Proof-of-payment types should NOT be merged.
    /// </summary>
    public static bool IsReceiptType(string documentType)
    {
        return documentType == "TaxInvoiceOrReceipt";
    }
}