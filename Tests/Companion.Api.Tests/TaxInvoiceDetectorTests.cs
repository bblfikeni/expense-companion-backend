using Companion.Extraction.Logic;

namespace Companion.Api.Tests;

public class TaxInvoiceDetectorTests
{
    [Theory]
    [InlineData("TAX INVOICE", true)]
    [InlineData("Belastingfaktuur", true)]
    [InlineData("Pro-forma Tax Invoice", false)]
    [InlineData("Quote", false)]
    [InlineData("Estimate", false)]
    [InlineData("VAT number 4123456789", null)]
    public void RequiresWording(string text, bool? expected) => Assert.Equal(expected, TaxInvoiceDetector.Detect(text));
}