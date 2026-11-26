using Companion.Extraction.Logic;

namespace Companion.Api.Tests;

public class AmountParserTests
{
    [Theory]
    [InlineData("R1 234,50", 123450L)]
    [InlineData("R1,234.50", 123450L)]
    [InlineData("R1.234,50", 123450L)]
    [InlineData("123", 12300L)]
    public void ParsesAmounts(string input, long expected) => Assert.Equal(expected, AmountParser.Parse(input));

    [Theory]
    [InlineData("not an amount")]
    [InlineData("1,23,45")]
    [InlineData("999999999999999999999999999")]
    public void RejectsInvalidAmounts(string input) => Assert.Null(AmountParser.Parse(input));
}