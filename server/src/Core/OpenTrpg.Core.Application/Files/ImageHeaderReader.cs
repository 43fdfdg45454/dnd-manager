using System.Buffers.Binary;

namespace OpenTrpg.Core.Application.Files;

/// <summary>Pixel size of an image.</summary>
public readonly record struct ImageSize(int Width, int Height);

/// <summary>
/// Reads the pixel size of PNG, JPEG and WebP images from their headers without decoding them (no
/// imaging dependency needed). The stream must be seekable.
/// </summary>
public static class ImageHeaderReader
{
    public static ReadOnlySpan<byte> PngSignature => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    /// <summary>The size of the image, or null when the header is malformed or the size is zero.</summary>
    public static ImageSize? Read(Stream stream, DetectedFileType type)
    {
        try
        {
            stream.Position = 0;
            var size = type switch
            {
                DetectedFileType.Png => ReadPng(stream),
                DetectedFileType.Jpeg => ReadJpeg(stream),
                DetectedFileType.WebP => ReadWebP(stream),
                _ => null,
            };
            return size is { Width: > 0, Height: > 0 } ? size : null;
        }
        catch (EndOfStreamException)
        {
            return null;
        }
        finally
        {
            stream.Position = 0;
        }
    }

    private static ImageSize? ReadPng(Stream stream)
    {
        // Signature (8), then the IHDR chunk: length (4), "IHDR" (4), width (4), height (4).
        Span<byte> header = stackalloc byte[24];
        stream.ReadExactly(header);
        if (!header[..8].SequenceEqual(PngSignature) || !header[12..16].SequenceEqual("IHDR"u8))
        {
            return null;
        }

        var width = BinaryPrimitives.ReadUInt32BigEndian(header[16..20]);
        var height = BinaryPrimitives.ReadUInt32BigEndian(header[20..24]);
        return width > int.MaxValue || height > int.MaxValue ? null : new ImageSize((int)width, (int)height);
    }

    private static ImageSize? ReadJpeg(Stream stream)
    {
        // Walk the marker segments until a start-of-frame (SOFn) one, which holds the size.
        stream.Position = 2;
        Span<byte> buffer = stackalloc byte[7];
        while (true)
        {
            int marker;
            do
            {
                marker = stream.ReadByte();
            }
            while (marker != 0xFF && marker != -1);

            while (marker == 0xFF)
            {
                marker = stream.ReadByte();
            }

            if (marker == -1)
            {
                return null;
            }

            // Markers without a payload: TEM, RSTn, SOI, and a stuffed 0x00.
            if (marker is 0x00 or 0x01 or (>= 0xD0 and <= 0xD8))
            {
                continue;
            }

            // End of image or start of scan before any frame header: malformed.
            if (marker is 0xD9 or 0xDA)
            {
                return null;
            }

            stream.ReadExactly(buffer[..2]);
            var length = BinaryPrimitives.ReadUInt16BigEndian(buffer);
            if (length < 2)
            {
                return null;
            }

            var isFrame = marker is >= 0xC0 and <= 0xCF && marker is not (0xC4 or 0xC8 or 0xCC);
            if (isFrame)
            {
                // Precision (1), height (2), width (2).
                stream.ReadExactly(buffer[..5]);
                return new ImageSize(BinaryPrimitives.ReadUInt16BigEndian(buffer[3..5]), BinaryPrimitives.ReadUInt16BigEndian(buffer[1..3]));
            }

            stream.Seek(length - 2, SeekOrigin.Current);
        }
    }

    private static ImageSize? ReadWebP(Stream stream)
    {
        // RIFF (4), size (4), "WEBP" (4), then the first chunk: fourcc (4), size (4), payload.
        // Tiny files can be shorter than the buffer: the missing bytes stay zero and fail the checks below.
        Span<byte> header = stackalloc byte[30];
        header.Clear();
        stream.ReadAtLeast(header, header.Length, throwOnEndOfStream: false);
        if (!header[..4].SequenceEqual("RIFF"u8) || !header[8..12].SequenceEqual("WEBP"u8))
        {
            return null;
        }

        var fourCc = header[12..16];
        var payload = header[20..];
        if (fourCc.SequenceEqual("VP8 "u8))
        {
            // Lossy: 3-byte frame tag, start code 9D 01 2A, then two 14-bit little-endian values.
            if (payload[3] != 0x9D || payload[4] != 0x01 || payload[5] != 0x2A)
            {
                return null;
            }

            return new ImageSize(
                BinaryPrimitives.ReadUInt16LittleEndian(payload[6..8]) & 0x3FFF,
                BinaryPrimitives.ReadUInt16LittleEndian(payload[8..10]) & 0x3FFF);
        }

        if (fourCc.SequenceEqual("VP8L"u8))
        {
            // Lossless: signature 0x2F, then 14 bits of (width - 1) and 14 bits of (height - 1).
            if (payload[0] != 0x2F)
            {
                return null;
            }

            var bits = BinaryPrimitives.ReadUInt32LittleEndian(payload[1..5]);
            return new ImageSize((int)(bits & 0x3FFF) + 1, (int)((bits >> 14) & 0x3FFF) + 1);
        }

        if (fourCc.SequenceEqual("VP8X"u8))
        {
            // Extended: flags (1), reserved (3), then 24-bit (canvas width - 1) and (canvas height - 1).
            var width = payload[4] | (payload[5] << 8) | (payload[6] << 16);
            var height = payload[7] | (payload[8] << 8) | (payload[9] << 16);
            return new ImageSize(width + 1, height + 1);
        }

        return null;
    }
}
