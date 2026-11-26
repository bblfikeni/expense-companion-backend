using Companion.Extraction.Localization;
using Companion.Extraction.Logic;
using Companion.Extraction.Ocr;

namespace Companion.Api.Tests;

public class ReceiptMergerTests
{
    [Fact]
    public void PreservesPageAndLineOrderForExtractors()
    {
        var lines = new List<OcrResult.OcrLineResult>
        {
            new() { PageNo = 2, Text = "VAT: R15.00" },
            new() { PageNo = 1, Text = "Zebra Shop" },
            new() { PageNo = 1, Text = "Apple item" },
            new() { PageNo = 2, Text = "Total R115.00" }
        };

        var text = ReceiptMerger.MergeI(lines);
        Assert.Equal("Zebra Shop\nApple item\nVAT: R15.00\nTotal R115.00", text);
        Assert.Equal("Zebra Shop", VendorExtractor.Extract(text).Vendor);
        Assert.Equal(1500, VatAmountExtractor.Extract(text));
    }
}