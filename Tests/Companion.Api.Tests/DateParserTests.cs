using Companion.Extraction.Logic;

namespace Companion.Api.Tests;

public class DateParserTests
{
    private static readonly DateOnly Today = new(2026, 11, 26);

    [Theory]
    [InlineData("26/11/2026")]
    [InlineData("2026-11-26")]
    [InlineData("26 Nov 2026")]
    [InlineData("26 Mrt 2026")]
    [InlineData("26 Mei 2026")]
    [InlineData("26 Okt 2026")]
    [InlineData("26 Des 2025")]
    public void ParsesSupportedDates(string input) => Assert.NotNull(DateParser.Parse(input, Today));

    [Theory]
    [InlineData("27/11/2026")]
    [InlineData("26/11/2020")]
    [InlineData("31/02/2025")]
    public void RejectsInvalidOrOutOfRangeDates(string input) => Assert.Null(DateParser.Parse(input, Today));

    [Fact]
    public void IncludesFiveYearBoundary() => Assert.Equal(new DateOnly(2021, 11, 26), DateParser.Parse("26/11/2021", Today));
}