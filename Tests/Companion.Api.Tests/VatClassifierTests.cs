using Companion.Extraction.Logic;
using Companion.Extraction.Configuration;
using Microsoft.Extensions.Configuration;

namespace Companion.Api.Tests;

public class VatClassifierTests
{
    [Theory]
    [InlineData("", 11500, 1500, "Standard_15", true)]
    [InlineData("", 11500, 1505, "Standard_15", true)]
    [InlineData("", 11500, 1506, null, false)]
    [InlineData("zero-rated items", 11500, 1000, null, true)]
    [InlineData("zero-rated", 10000, 0, "Zero_Rated", true)]
    [InlineData("exempt", 10000, 0, "Exempt", true)]
    [InlineData("", 10000, 0, null, true)]
    public void ClassifiesConservatively(string text, long total, long vat, string? classification, bool consistent)
        => Assert.Equal((classification, consistent), VatClassifier.Classify(text, total, vat));

    [Fact]
    public void MissingVatIsNotInferred() => Assert.Equal((null, false), VatClassifier.Classify("", 11500, null));

    [Fact]
    public void UsesConfiguredToleranceInCents()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["VatToleranceCents"] = "10"
        }).Build();
        var options = configuration.Get<ExtractionOptions>()!;
        Assert.Equal(("Standard_15", true), VatClassifier.Classify("", 11500, 1508, options));
        Assert.Equal((null, false), VatClassifier.Classify("", 11500, 1511, options));
    }
}