using System.Buffers.Binary;

namespace Companion.Api.Features.Images;

public readonly record struct ImageDimensions(int Width, int Height);

public static class ImageHeader
{
    private const int MaxDimension = 16384;
    private const long MaxPixels = 40_000_000;

    public static bool TryIdentify(ReadOnlySpan<byte> bytes, string? contentType, out ImageDimensions dimensions)
    {
        dimensions = default;
        ImageDimensions identified;
        bool valid;
        if (string.Equals(contentType, "image/png", StringComparison.OrdinalIgnoreCase))
            valid = TryPng(bytes, out identified);
        else if (string.Equals(contentType, "image/jpeg", StringComparison.OrdinalIgnoreCase))
            valid = TryJpeg(bytes, out identified);
        else if (string.Equals(contentType, "image/webp", StringComparison.OrdinalIgnoreCase))
            valid = TryWebp(bytes, out identified);
        else return false;
        if (!valid || identified.Width > MaxDimension || identified.Height > MaxDimension ||
            (long)identified.Width * identified.Height > MaxPixels) return false;
        dimensions = identified;
        return true;
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
        var offset = 33;
        var hasData = false;
        while (data.Length - offset >= 12)
        {
            var size = BinaryPrimitives.ReadUInt32BigEndian(data[offset..]);
            if (size > (uint)(data.Length - offset - 12)) return false;
            var type = data.Slice(offset + 4, 4);
            var count = (int)size;
            if (Crc32(data.Slice(offset + 4, count + 4)) != BinaryPrimitives.ReadUInt32BigEndian(data[(offset + 8 + count)..])) return false;
            if (type.SequenceEqual("IDAT"u8) && size > 0) hasData = true;
            if (type.SequenceEqual("IEND"u8))
            {
                if (size != 0 || !hasData || offset + 12 != data.Length) return false;
                dimensions = new((int)width, (int)height);
                return true;
            }
            offset += count + 12;
        }
        return false;
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
        var foundFrame = false;
        var foundScan = false;
        var inScan = false;
        var scanData = false;
        ImageDimensions frame = default;
        while (offset < data.Length)
        {
            if (inScan)
            {
                while (offset < data.Length && data[offset] != 0xff)
                {
                    scanData = true;
                    offset++;
                }
                if (offset >= data.Length - 1) return false;
                if (data[offset + 1] == 0 || data[offset + 1] is >= 0xd0 and <= 0xd7)
                {
                    if (data[offset + 1] == 0) scanData = true;
                    offset += 2;
                    continue;
                }
                if (!scanData) return false;
                inScan = false;
            }
            if (data[offset++] != 0xff) return false;
            while (offset < data.Length && data[offset] == 0xff) offset++;
            if (offset >= data.Length) return false;
            var marker = data[offset++];
            if (marker == 0xd9)
            {
                if (!foundFrame || !foundScan || offset != data.Length) return false;
                dimensions = frame;
                return true;
            }
            if (marker is 0 or 0xd8 || marker is >= 0xd0 and <= 0xd7) return false;
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
                if (foundFrame) return false;
                frame = new(width, height);
                foundFrame = true;
            }
            if (marker == 0xda)
            {
                if (!foundFrame || length < 6 || data[offset + 2] == 0 || length != 6 + 2 * data[offset + 2]) return false;
                foundScan = true;
                inScan = true;
                scanData = false;
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
            var offset = 30;
            var hasImage = false;
            while (data.Length - offset >= 8)
            {
                var size = BinaryPrimitives.ReadUInt32LittleEndian(data[(offset + 4)..]);
                var total = 8UL + size + (size & 1);
                if (total > (ulong)(data.Length - offset)) return false;
                var type = data.Slice(offset, 4);
                if (type.SequenceEqual("VP8 "u8) || type.SequenceEqual("VP8L"u8))
                {
                    if (!ValidWebpImage(type, data.Slice(offset + 8, (int)size))) return false;
                    hasImage = true;
                }
                else if (type.SequenceEqual("ANMF"u8))
                {
                    if (size < 24 || !ValidWebpFrame(data.Slice(offset + 24, (int)size - 16))) return false;
                    hasImage = true;
                }
                offset += (int)total;
            }
            if (!hasImage || offset != data.Length) return false;
        }
        else return false;
        if (!data.Slice(12, 4).SequenceEqual("VP8X"u8) && (ulong)chunkSize + 20 + (chunkSize & 1) != (ulong)data.Length) return false;
        if (width == 0 || height == 0) return false;
        dimensions = new(width, height);
        return true;
    }

    private static bool ValidWebpFrame(ReadOnlySpan<byte> data)
    {
        var offset = 0;
        var hasImage = false;
        while (data.Length - offset >= 8)
        {
            var size = BinaryPrimitives.ReadUInt32LittleEndian(data[(offset + 4)..]);
            var total = 8UL + size + (size & 1);
            if (total > (ulong)(data.Length - offset)) return false;
            var type = data.Slice(offset, 4);
            if (type.SequenceEqual("VP8 "u8) || type.SequenceEqual("VP8L"u8))
            {
                if (!ValidWebpImage(type, data.Slice(offset + 8, (int)size))) return false;
                hasImage = true;
            }
            offset += (int)total;
        }
        return hasImage && offset == data.Length;
    }

    private static bool ValidWebpImage(ReadOnlySpan<byte> type, ReadOnlySpan<byte> payload) =>
        type.SequenceEqual("VP8 "u8)
            ? payload.Length >= 10 && (payload[0] & 1) == 0 && payload.Slice(3, 3).SequenceEqual(new byte[] { 0x9d, 0x01, 0x2a }) &&
              (BinaryPrimitives.ReadUInt16LittleEndian(payload[6..]) & 0x3fff) != 0 && (BinaryPrimitives.ReadUInt16LittleEndian(payload[8..]) & 0x3fff) != 0
            : payload.Length >= 5 && payload[0] == 0x2f && (payload[4] & 0xe0) == 0;
}