using Dnd.Application.Files;
using static Dnd.Api.Tests.Content.ContentTestHelpers;

namespace Dnd.Api.Tests.Content;

public sealed class ImageHeaderReaderTests
{
    private static ImageSize? Read(byte[] bytes, DetectedFileType type) => ImageHeaderReader.Read(new MemoryStream(bytes), type);

    private static DetectedFileType? Detect(byte[] bytes) => FileContent.Detect(new MemoryStream(bytes));

    [Fact]
    public void Reads_png_size()
    {
        Assert.Equal(new ImageSize(1920, 1080), Read(Png(1920, 1080), DetectedFileType.Png));
    }

    [Fact]
    public void Reads_jpeg_size_skipping_other_segments()
    {
        Assert.Equal(new ImageSize(4000, 3000), Read(Jpeg(4000, 3000), DetectedFileType.Jpeg));
    }

    [Fact]
    public void Reads_lossless_webp_size_even_when_the_file_is_tiny()
    {
        Assert.Equal(new ImageSize(320, 200), Read(WebpLossless(320, 200), DetectedFileType.WebP));
        Assert.Equal(new ImageSize(1, 1), Read(WebpLossless(1, 1)[..25], DetectedFileType.WebP));
    }

    [Fact]
    public void Reads_lossy_and_extended_webp_size()
    {
        byte[] lossy = [.. "RIFF"u8, 0x30, 0, 0, 0, .. "WEBPVP8 "u8, 0x10, 0, 0, 0, 0x10, 0x02, 0x00, 0x9D, 0x01, 0x2A, 0x40, 0x01, 0xC8, 0x00, 0, 0, 0, 0];
        byte[] extended = [.. "RIFF"u8, 0x30, 0, 0, 0, .. "WEBPVP8X"u8, 0x0A, 0, 0, 0, 0, 0, 0, 0, 0xFF, 0x03, 0x00, 0xFF, 0x01, 0x00];

        Assert.Equal(new ImageSize(320, 200), Read(lossy, DetectedFileType.WebP));
        Assert.Equal(new ImageSize(1024, 512), Read(extended, DetectedFileType.WebP));
    }

    [Fact]
    public void Truncated_or_malformed_headers_have_no_size()
    {
        Assert.Null(Read(Png()[..20], DetectedFileType.Png));
        Assert.Null(Read(Jpeg()[..10], DetectedFileType.Jpeg));
        Assert.Null(Read([0xFF, 0xD8, 0xFF, 0xD9], DetectedFileType.Jpeg));
        Assert.Null(Read(WebpLossless()[..8], DetectedFileType.WebP));
        Assert.Null(Read(Png(0, 10), DetectedFileType.Png));
        Assert.Null(Read([], DetectedFileType.Png));
    }

    [Fact]
    public void Detects_the_type_from_the_first_bytes()
    {
        Assert.Equal(DetectedFileType.Png, Detect(Png()));
        Assert.Equal(DetectedFileType.Jpeg, Detect(Jpeg()));
        Assert.Equal(DetectedFileType.WebP, Detect(WebpLossless()));
        Assert.Equal(DetectedFileType.Pdf, Detect(Pdf()));
        Assert.Equal(DetectedFileType.Zip, Detect(Apk()));
        Assert.Null(Detect("MZ executable"u8.ToArray()));
        Assert.Null(Detect([]));
    }
}
