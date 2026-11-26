using Companion.Extraction.Logic;

namespace Companion.Api.Tests;

public class VatAmountExtractorTests
{
    [Theory]
    [InlineData("Total R115.00\nVAT amount: R15.00", 1500L)]
    [InlineData("BTW bedrag: R15,00", 1500L)]
    public void ReadsPrintedVat(string text, long expected) => Assert.Equal(expected, VatAmountExtractor.Extract(text));

    [Theory]
    [InlineData("Total R115.00")]
    [InlineData("VAT number: 4123456789")]
    [InlineData("VAT 15%")]
    public void DoesNotInferVat(string text) => Assert.Null(VatAmountExtractor.Extract(text));
}