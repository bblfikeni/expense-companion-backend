using Companion.Extraction.Localization;

namespace Companion.Api.Tests;

public class DocClassifierTests
{
    [Theory]
    [InlineData("TAX INVOICE", true)]
    [InlineData("belastingfaktuur", true)]
    [InlineData("Pro-forma Tax Invoice", false)]
    [InlineData("R100.00 2026-11-26", false)]
    [InlineData("official receipt", false)]
    public void RequiresExplicitTaxInvoiceWording(string text, bool expected)
        => Assert.Equal(expected, DocClassifier.Classify(text, new()).IsTaxInvoice);

    [Theory]
    [InlineData("eft transfer", "EftConfirmation")]
    [InlineData("pos", "CardMachineSlip")]
    [InlineData("QUOTE", "Unknown")]
    public void MatchesConfiguredKeywordsIgnoringCase(string text, string expected)
        => Assert.Equal(expected, DocClassifier.Classify(text, new()).DocType);
}