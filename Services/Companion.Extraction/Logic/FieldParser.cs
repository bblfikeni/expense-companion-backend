using Companion.Extraction.Configuration;
using Companion.Extraction.Ocr;

namespace Companion.Extraction.Logic;

public static class FieldParser
{
    public static ParsedReceipt Parse(string text, IReadOnlyCollection<OcrResult.OcrWordResult> tokens,
        DateOnly today, ExtractionOptions options)
    {
        var vendor = VendorExtractor.Extract(text);
        var dateText = ReceiptFieldSelector.Date(text);
        var date = DateParser.Parse(dateText, today);
        var totalText = ReceiptFieldSelector.Total(text);
        var total = AmountParser.Parse(totalText);
        var vat = VatAmountExtractor.Extract(text);
        var vatNumberText = ReceiptFieldSelector.VatNumber(text);
        var vatNumber = VatNumberValidator.IsValid(vatNumberText) ? vatNumberText : null;
        var classification = VatClassifier.Classify(text, total, vat, options);
        var validity = TaxInvoiceDetector.Detect(text);
        return new ParsedReceipt(vendor.Vendor, date, total, vat, vatNumber, classification.Classification, validity,
            ReceiptTokenConfidence.For(vendor.Vendor, tokens),
            ReceiptTokenConfidence.For(date is null ? null : dateText, tokens),
            ReceiptTokenConfidence.For(total is null ? null : totalText, tokens),
            ReceiptTokenConfidence.For(vat is null ? null : ReceiptFieldSelector.VatAmount(text), tokens),
            ReceiptTokenConfidence.For(vatNumber, tokens),
            ReceiptTokenConfidence.For(validity is null ? null : ReceiptFieldSelector.DocumentValidity(text), tokens));
    }
}