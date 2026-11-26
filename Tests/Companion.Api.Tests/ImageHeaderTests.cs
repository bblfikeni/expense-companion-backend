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

    [Theory]
    [InlineData("png")]
    [InlineData("jpeg")]
    [InlineData("webp")]
    public void RejectsEveryTruncatedFile(string name)
    {
        var bytes = Fixture(name);
        var type = $"image/{name}";
        for (var length = 0; length < bytes.Length; length++)
            Assert.False(ImageHeader.TryIdentify(bytes.AsSpan(0, length), type, out _));
    }

    [Theory]
    [InlineData(16384, 1, true)]
    [InlineData(16385, 1, false)]
    [InlineData(1, 16385, false)]
    [InlineData(8000, 5000, true)]
    [InlineData(8001, 5000, false)]
    public void BoundsDimensions(int width, int height, bool expected)
    {
        var jpeg = (byte[])Jpeg.Clone();
        BinaryPrimitives.WriteUInt16BigEndian(jpeg.AsSpan(25), (ushort)height);
        BinaryPrimitives.WriteUInt16BigEndian(jpeg.AsSpan(27), (ushort)width);
        Assert.Equal(expected, ImageHeader.TryIdentify(jpeg, "image/jpeg", out _));
    }

    [Fact]
    public void RejectsExtendedWebpWithoutImageChunk()
    {
        var extended = Convert.FromHexString("524946461600000057454250565038580A00000000000000020000010000");
        Assert.False(ImageHeader.TryIdentify(extended, "image/webp", out _));
    }

    [Fact]
    public void BoundsPngAndExtendedWebpDimensions()
    {
        var png = (byte[])Png.Clone();
        BinaryPrimitives.WriteUInt32BigEndian(png.AsSpan(16), 16385);
        uint crc = uint.MaxValue;
        foreach (var value in png.AsSpan(12, 17))
        {
            crc ^= value;
            for (var i = 0; i < 8; i++) crc = (crc >> 1) ^ (0xedb88320u & (uint)-(int)(crc & 1));
        }
        BinaryPrimitives.WriteUInt32BigEndian(png.AsSpan(29), ~crc);
        Assert.False(ImageHeader.TryIdentify(png, "image/png", out _));

        var webp = Convert.FromHexString("524946462800000057454250565038580A00000000000000004000010000565038200A0000001000009D012A03000200");
        Assert.False(ImageHeader.TryIdentify(webp, "image/webp", out _));
    }

    [Fact]
    public void RejectsMalformedExtendedWebpImageChunk()
    {
        var webp = Convert.FromHexString("524946462800000057454250565038580A00000000000000020000010000565038200A00000010000000000003000200");
        Assert.False(ImageHeader.TryIdentify(webp, "image/webp", out _));
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
        var extended = Convert.FromHexString("524946462800000057454250565038580A00000000000000020000010000565038200A0000001000009D012A03000200");
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