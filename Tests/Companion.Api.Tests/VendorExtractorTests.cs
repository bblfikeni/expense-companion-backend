using Companion.Extraction.Logic;

namespace Companion.Api.Tests;

public class VendorExtractorTests
{
    [Fact]
    public void PrefersFirstClearTopLine() => Assert.Equal(("Invented Shop", 0.6), VendorExtractor.Extract("\nTax invoice\nInvented Shop\nOther Store"));

    [Fact]
    public void UnclearReceiptReturnsLowConfidence() => Assert.Equal((null, 0d), VendorExtractor.Extract("Tax Invoice\n123\nVAT 123"));
}