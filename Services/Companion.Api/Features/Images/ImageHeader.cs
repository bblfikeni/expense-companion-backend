using System.Buffers.Binary;

namespace Companion.Api.Features.Images;

public readonly record struct ImageDimensions(int Width, int Height);

public static class ImageHeader
{
    public static bool TryIdentify(ReadOnlySpan<byte> bytes, string? contentType, out ImageDimensions dimensions)
    {
        dimensions = default;
        if (string.Equals(contentType, "image/png", StringComparison.OrdinalIgnoreCase))
            return TryPng(bytes, out dimensions);
        if (string.Equals(contentType, "image/jpeg", StringComparison.OrdinalIgnoreCase))
            return TryJpeg(bytes, out dimensions);
        if (string.Equals(contentType, "image/webp", StringComparison.OrdinalIgnoreCase))
            return TryWebp(bytes, out dimensions);
        return false;
    }

    private static bool TryPng(ReadOnlySpan<byte> data, out ImageDimensions dimensions)
    {
        dimensions = default;
        if (data.Length < 33 || data[0] != 0x89 || !data.Slice(1, 7).SequenceEqual("PNG\r\n\x1a\n"u8) ||
            BinaryPrimitives.ReadUInt32BigEndian(data[8..]) != 13 || !data.Slice(12, 4).SequenceEqual("IHDR"u8))
            return false;

        var width = BinaryPrimitives.ReadUInt32BigEndian(data[16..]);
        var height = BinaryPrimitives.ReadUInt32BigEndian(data[20..]);
        var depth = data[24];
        var validDepth = data[25] switch
        {
            0 => depth is 1 or 2 or 4 or 8 or 16,
            2 or 4 or 6 => depth is 8 or 16,
            3 => depth is 1 or 2 or 4 or 8,
            _ => false
        };
        if (width is 0 or > int.MaxValue || height is 0 or > int.MaxValue || !validDepth ||
            data[26] != 0 || data[27] != 0 || data[28] > 1 ||
            Crc32(data.Slice(12, 17)) != BinaryPrimitives.ReadUInt32BigEndian(data[29..]))
            return false;
        dimensions = new((int)width, (int)height);
        return true;
    }

    private static uint Crc32(ReadOnlySpan<byte> data)
    {
        uint crc = uint.MaxValue;
        foreach (var value in data)
        {
            crc ^= value;
            for (var i = 0; i < 8; i++)
                crc = (crc >> 1) ^ (0xedb88320u & (uint)-(int)(crc & 1));
        }
        return ~crc;
    }

    private static bool TryJpeg(ReadOnlySpan<byte> data, out ImageDimensions dimensions)
    {
        dimensions = default;
        if (data.Length < 4 || data[0] != 0xff || data[1] != 0xd8)
            return false;
        var offset = 2;
        while (offset < data.Length)
        {
            if (data[offset++] != 0xff) return false;
            while (offset < data.Length && data[offset] == 0xff) offset++;
            if (offset >= data.Length) return false;
            var marker = data[offset++];
            if (marker is 0 or 0xd8 or 0xd9 or 0xda || marker is >= 0xd0 and <= 0xd7) return false;
            if (marker == 0x01) continue;
            if (data.Length - offset < 2) return false;
            var length = BinaryPrimitives.ReadUInt16BigEndian(data[offset..]);
            if (length < 2 || length > data.Length - offset) return false;
            if (marker is >= 0xc0 and <= 0xcf && marker is not (0xc4 or 0xc8 or 0xcc))
            {
                if (length < 11) return false;
                var height = BinaryPrimitives.ReadUInt16BigEndian(data[(offset + 3)..]);
                var width = BinaryPrimitives.ReadUInt16BigEndian(data[(offset + 5)..]);
                var components = data[offset + 7];
                if (width == 0 || height == 0 || components == 0 || length != 8 + 3 * components) return false;
                dimensions = new(width, height);
                return true;
            }
            offset += length;
        }
        return false;
    }

    private static bool TryWebp(ReadOnlySpan<byte> data, out ImageDimensions dimensions)
    {
        dimensions = default;
        if (data.Length < 30 || !data[..4].SequenceEqual("RIFF"u8) || !data.Slice(8, 4).SequenceEqual("WEBP"u8))
            return false;
        var riffSize = BinaryPrimitives.ReadUInt32LittleEndian(data[4..]);
        var chunkSize = BinaryPrimitives.ReadUInt32LittleEndian(data[16..]);
        if ((ulong)riffSize + 8 != (ulong)data.Length || (ulong)chunkSize + 20 + (chunkSize & 1) > (ulong)data.Length)
            return false;
        int width, height;
        if (data.Slice(12, 4).SequenceEqual("VP8 "u8))
        {
            if (chunkSize < 10 || (data[20] & 1) != 0 || !data.Slice(23, 3).SequenceEqual(new byte[] { 0x9d, 0x01, 0x2a })) return false;
            width = BinaryPrimitives.ReadUInt16LittleEndian(data[26..]) & 0x3fff;
            height = BinaryPrimitives.ReadUInt16LittleEndian(data[28..]) & 0x3fff;
        }
        else if (data.Slice(12, 4).SequenceEqual("VP8L"u8))
        {
            if (chunkSize < 5 || data[20] != 0x2f || (data[24] & 0xe0) != 0) return false;
            var bits = BinaryPrimitives.ReadUInt32LittleEndian(data[21..]);
            width = (int)(bits & 0x3fff) + 1;
            height = (int)((bits >> 14) & 0x3fff) + 1;
        }
        else if (data.Slice(12, 4).SequenceEqual("VP8X"u8))
        {
            if (chunkSize != 10 || (data[20] & 0xc1) != 0 || data[21] != 0 || data[22] != 0 || data[23] != 0) return false;
            width = 1 + data[24] + (data[25] << 8) + (data[26] << 16);
            height = 1 + data[27] + (data[28] << 8) + (data[29] << 16);
        }
        else return false;
        if (width == 0 || height == 0) return false;
        dimensions = new(width, height);
        return true;
    }
}