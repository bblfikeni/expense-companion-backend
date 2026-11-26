using Companion.Extraction.Logic;

namespace Companion.Api.Tests;

public class VatNumberValidatorTests
{
    [Theory]
    [InlineData("4123456789", true)]
    [InlineData("3123456789", false)]
    [InlineData("412345678", false)]
    [InlineData("412345678a", false)]
    public void ChecksFormatOnly(string value, bool expected) => Assert.Equal(expected, VatNumberValidator.IsValid(value));
}