using Companion.Extraction.Configuration;

namespace Companion.Api.Tests;

public sealed class ExtractionOptionsTests
{
    [Theory]
    [InlineData(0, false)]
    [InlineData(9, false)]
    [InlineData(10, true)]
    [InlineData(11, false)]
    public void ImageLimitMustMatchGatewayLimit(int maximumImages, bool expected)
    {
        Assert.Equal(expected, ExtractionOptions.HasCompatibleImageLimit(maximumImages));
    }
}