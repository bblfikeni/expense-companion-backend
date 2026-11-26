using System.Buffers.Binary;
using Companion.Api.Features.Images;

namespace Companion.Api.Tests;

public class ImageHeaderTests
{
    private static readonly byte[] Png = Convert.FromHexString("89504E470D0A1A0A0000000D49484452000000010000000108060000001F15C4890000000D49444154789C6360606060000000050001A5F645400000000049454E44AE426082");
    private static readonly byte[] Jpeg = Convert.FromHexString("FFD8FFE000104A46494600010100000100010000FFC00011080002000303011100021101031101FFDA000C03010002110311003F0000FFD9");
    private static readonly byte[] Webp = Convert.FromHexString("5249464616000000574542505650384C090000002F024000000000000000");

    [Theory]
    [InlineData("png", "image/png", 1, 1)]
    [InlineData("jpeg", "image/jpeg", 3, 2)]
    [InlineData("webp", "image/webp", 3, 2)]
    public void IdentifiesSupportedHeaders(string name, string contentType, int width, int height)
    {
        Assert.True(ImageHeader.TryIdentify(Fixture(name), contentType, out var dimensions));
        Assert.Equal(new ImageDimensions(width, height), dimensions);
    }

    [Theory]
    [InlineData("png", "image/jpeg")]
    [InlineData("jpeg", "image/webp")]
    [InlineData("webp", "image/png")]
    [InlineData("png", "image/heic")]
    [InlineData("png", "image/png; charset=utf-8")]
    public void RejectsWrongOrUnsupportedDeclaredType(string name, string contentType) =>
        Assert.False(ImageHeader.TryIdentify(Fixture(name), contentType, out _));

    [Theory]
    [InlineData("png")]
    [InlineData("jpeg")]
    [InlineData("webp")]
    public void RejectsEveryTruncatedHeader(string name)
    {
        var bytes = Fixture(name);
        var type = name == "jpeg" ? "image/jpeg" : $"image/{name}";
        var minimum = name switch { "png" => 33, "jpeg" => 39, _ => 30 };
        for (var length = 0; length < minimum; length++)
            Assert.False(ImageHeader.TryIdentify(bytes.AsSpan(0, length), type, out _));
    }

    [Fact]
    public void RejectsUnsupportedFormatsAndCorruptHeaders()
    {
        Assert.False(ImageHeader.TryIdentify("%PDF-1.7"u8, "image/jpeg", out _));
        Assert.False(ImageHeader.TryIdentify(Convert.FromHexString("0000001866747970686569630000000068656963"), "image/heic", out _));
        var png = (byte[])Png.Clone();
        png[19] = 0;
        Assert.False(ImageHeader.TryIdentify(png, "image/png", out _));
        var jpeg = (byte[])Jpeg.Clone();
        jpeg[5] = 0xff;
        Assert.False(ImageHeader.TryIdentify(jpeg, "image/jpeg", out _));
        var webp = (byte[])Webp.Clone();
        BinaryPrimitives.WriteUInt32LittleEndian(webp.AsSpan(4), uint.MaxValue);
        Assert.False(ImageHeader.TryIdentify(webp, "image/webp", out _));
    }

    [Fact]
    public void IdentifiesLossyAndExtendedWebp()
    {
        var lossy = Convert.FromHexString("524946461600000057454250565038200A0000001000009D012A03000200");
        var extended = Convert.FromHexString("524946461600000057454250565038580A00000000000000020000010000");
        Assert.True(ImageHeader.TryIdentify(lossy, "image/webp", out var first));
        Assert.Equal(new ImageDimensions(3, 2), first);
        Assert.True(ImageHeader.TryIdentify(extended, "IMAGE/WEBP", out var second));
        Assert.Equal(first, second);
    }

    private static byte[] Fixture(string name) => name switch
    {
        "png" => Png,
        "jpeg" => Jpeg,
        _ => Webp
    };
}