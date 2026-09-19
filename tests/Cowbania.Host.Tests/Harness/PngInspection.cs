using System.Buffers.Binary;
using System.IO.Compression;

namespace Cowbania.Host.Tests.Harness;

internal static class PngInspection
{
    public static PngInfo ReadPng(string path)
    {
        var bytes = File.ReadAllBytes(path);
        Assert(bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }),
            $"{path} has an invalid PNG signature");
        var offset = 8;
        var idat = new MemoryStream();
        var width = 0;
        var height = 0;
        byte bitDepth = 0, colorType = 0, interlace = 0;
        while (offset + 12 <= bytes.Length)
        {
            var length = BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(offset, 4));
            var type = System.Text.Encoding.ASCII.GetString(bytes, offset + 4, 4);
            var data = bytes.AsSpan(offset + 8, length);
            if (type == "IHDR")
            {
                width = BinaryPrimitives.ReadInt32BigEndian(data[..4]);
                height = BinaryPrimitives.ReadInt32BigEndian(data.Slice(4, 4));
                bitDepth = data[8];
                colorType = data[9];
                interlace = data[12];
            }
            else if (type == "IDAT")
                idat.Write(data);
            else if (type == "IEND")
                break;
            offset += length + 12;
        }
        Assert(width > 0 && height > 0 && idat.Length > 0, $"{path} is missing required PNG chunks");
        Assert(bitDepth == 8 && colorType == 6 && interlace == 0,
            $"{path} must be non-interlaced 8-bit RGBA before alpha validation");

        idat.Position = 0;
        using var zlib = new ZLibStream(idat, CompressionMode.Decompress);
        using var raw = new MemoryStream();
        zlib.CopyTo(raw);
        var scanlines = raw.ToArray();
        var stride = width * 4;
        Assert(scanlines.Length == (stride + 1) * height, $"{path} has an unexpected RGBA scanline length");
        var previous = new byte[stride];
        var current = new byte[stride];
        byte minimumAlpha = byte.MaxValue, maximumAlpha = byte.MinValue;
        for (var row = 0; row < height; row++)
        {
            var rowOffset = row * (stride + 1);
            var filter = scanlines[rowOffset];
            for (var column = 0; column < stride; column++)
            {
                var encoded = scanlines[rowOffset + 1 + column];
                var left = column >= 4 ? current[column - 4] : (byte)0;
                var up = previous[column];
                var upperLeft = column >= 4 ? previous[column - 4] : (byte)0;
                current[column] = filter switch
                {
                    0 => encoded,
                    1 => unchecked((byte)(encoded + left)),
                    2 => unchecked((byte)(encoded + up)),
                    3 => unchecked((byte)(encoded + ((left + up) >> 1))),
                    4 => unchecked((byte)(encoded + Paeth(left, up, upperLeft))),
                    _ => throw new InvalidDataException($"{path} uses unsupported PNG filter {filter}")
                };
            }
            for (var alpha = 3; alpha < stride; alpha += 4)
            {
                minimumAlpha = Math.Min(minimumAlpha, current[alpha]);
                maximumAlpha = Math.Max(maximumAlpha, current[alpha]);
            }
            (previous, current) = (current, previous);
            Array.Clear(current);
        }
        return new PngInfo(width, height, bitDepth, colorType, interlace, minimumAlpha, maximumAlpha);
    }

    private static byte Paeth(byte left, byte up, byte upperLeft)
    {
        var prediction = left + up - upperLeft;
        var leftDistance = Math.Abs(prediction - left);
        var upDistance = Math.Abs(prediction - up);
        var upperLeftDistance = Math.Abs(prediction - upperLeft);
        return leftDistance <= upDistance && leftDistance <= upperLeftDistance
            ? left
            : upDistance <= upperLeftDistance ? up : upperLeft;
    }
}

internal readonly record struct PngInfo(
    int Width,
    int Height,
    byte BitDepth,
    byte ColorType,
    byte InterlaceMethod,
    byte MinimumAlpha,
    byte MaximumAlpha);
