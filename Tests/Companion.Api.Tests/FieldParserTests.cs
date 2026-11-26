using Companion.Extraction.Configuration;
using Companion.Extraction.Logic;
using Companion.Extraction.Ocr;

namespace Companion.Api.Tests;

public class FieldParserTests
{
    private static readonly DateOnly Today = new(2026, 11, 26);

    private static ParsedReceipt Parse(string text) => FieldParser.Parse(text,
        text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .Select(word => new OcrResult.OcrWordResult { Text = word, Confidence = 0.8 }).ToArray(),
        Today, new ExtractionOptions());

    [Theory]
    [InlineData("Corner Shop\nTax Invoice\nDate 2026-01-02\nVAT No 4123456789\nTotal R115.00\nVAT R15.00", "Corner Shop", 11500, 1500)]
    [InlineData("Fuel Station\nTax Invoice\nDate 02/01/2026\nTotal R230.00\nVAT R30.00", "Fuel Station", 23000, 3000)]
    [InlineData("Winkel Naam\nBelastingfaktuur\nDatum 02 Mrt 2026\nBTW nr 4123456789\nTotaal R115,00\nBTW R15,00", "Winkel Naam", 11500, 1500)]
    public void ExtractsReceiptFields(string text, string vendor, long total, long vat)
    {
        var result = Parse(text);
        Assert.Equal(vendor, result.Vendor);
        Assert.NotNull(result.Date);
        Assert.Equal(total, result.TotalCents);
        Assert.Equal(vat, result.VatAmountCents);
        Assert.True(result.IsValidTaxInvoice);
        Assert.Equal("Standard_15", result.VatClassification);
        Assert.Equal(0.8, result.VendorConfidence, 5);
        Assert.Equal(0.8, result.DateConfidence, 5);
        Assert.Equal(0.8, result.TotalConfidence, 5);
        Assert.Equal(0.8, result.VatAmountConfidence, 5);
        Assert.Equal(0.8, result.DocumentValidityConfidence, 5);
    }

    [Theory]
    [InlineData("Pro-forma Tax Invoice")]
    [InlineData("Quote")]
    public void RejectsNonInvoices(string wording) => Assert.False(Parse($"Shop Name\n{wording}").IsValidTaxInvoice);

    [Fact]
    public void GarbledReceiptHasNoExtractedFields()
    {
        var result = Parse("??\n123");
        Assert.Null(result.Vendor);
        Assert.Null(result.Date);
        Assert.Null(result.TotalCents);
        Assert.Null(result.IsValidTaxInvoice);
        Assert.Equal(0, result.DocumentValidityConfidence);
    }

    [Theory]
    [InlineData("zero-rated", "R0.00", "Zero_Rated")]
    [InlineData("zero-rated items", "R5.00", null)]
    public void HandlesZeroAndMixedRates(string description, string vat, string? classification)
        => Assert.Equal(classification, Parse($"Shop Name\n{description}\nTotal R115.00\nVAT {vat}").VatClassification);

    [Fact]
    public void MissingVatIsNotDerived()
    {
        var result = Parse("Shop Name\nTotal R115.00");
        Assert.Null(result.VatAmountCents);
        Assert.Equal(0, result.VatAmountConfidence);
    }

    [Theory]
    [InlineData("2027-01-01")]
    [InlineData("2020-01-01")]
    public void RejectsOutOfRangeDates(string date) => Assert.Null(Parse($"Shop Name\nDate {date}").Date);

    [Fact]
    public void RejectsBadVatNumber() => Assert.Null(Parse("Shop Name\nVAT No 3123456789").VatNumber);

    [Fact]
    public void UsesMatchingTokenConfidenceForVatNumber()
    {
        var result = FieldParser.Parse("Shop Name\nVAT No 4123456789", [
            new() { Text = "Shop", Confidence = 0.1 },
            new() { Text = "4123456789", Confidence = 0.9 }
        ], Today, new ExtractionOptions());
        Assert.Equal("4123456789", result.VatNumber);
        Assert.Equal(0.9, result.VatNumberConfidence, 5);
    }

    [Theory]
    [InlineData("R1 234,50")]
    [InlineData("R1,234.50")]
    public void ParsesEquivalentTotals(string total) => Assert.Equal(123450, Parse($"Shop Name\nTotal {total}").TotalCents);
}