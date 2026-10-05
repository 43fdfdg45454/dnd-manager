using Dnd.Domain.Files;

namespace Dnd.Application.Files;

/// <summary>Kinds of content the server recognizes by looking at the first bytes of an upload.</summary>
public enum DetectedFileType
{
    Png,
    Jpeg,
    WebP,
    Pdf,
    Zip,
}

/// <summary>
/// Content sniffing and the type rules per <see cref="FileKind"/>. The type of an upload is decided
/// by its bytes, never by the (client supplied) content type or file name.
/// </summary>
public static class FileContent
{
    public const string Png = "image/png";
    public const string Jpeg = "image/jpeg";
    public const string WebP = "image/webp";
    public const string Pdf = "application/pdf";
    public const string Apk = "application/vnd.android.package-archive";

    /// <summary>Reads the signature at the start of the stream (which is rewound afterwards).</summary>
    public static DetectedFileType? Detect(Stream stream)
    {
        Span<byte> head = stackalloc byte[12];
        stream.Position = 0;
        var read = stream.ReadAtLeast(head, head.Length, throwOnEndOfStream: false);
        stream.Position = 0;
        var bytes = head[..read];

        if (bytes.StartsWith(ImageHeaderReader.PngSignature))
        {
            return DetectedFileType.Png;
        }

        if (bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
        {
            return DetectedFileType.Jpeg;
        }

        if (bytes.Length >= 12 && bytes[..4].SequenceEqual("RIFF"u8) && bytes[8..12].SequenceEqual("WEBP"u8))
        {
            return DetectedFileType.WebP;
        }

        if (bytes.StartsWith("%PDF-"u8))
        {
            return DetectedFileType.Pdf;
        }

        if (bytes.StartsWith("PK\x03\x04"u8))
        {
            return DetectedFileType.Zip;
        }

        return null;
    }

    /// <summary>
    /// The content type stored for a detected type when it is allowed for the kind, or null when it is
    /// not. Images are accepted for maps and portraits; images and PDF for lore attachments; PDF for
    /// the library; a ZIP container (APK) for app releases.
    /// </summary>
    public static string? ContentTypeFor(FileKind kind, DetectedFileType? detected) => (kind, detected) switch
    {
        (FileKind.MapImage or FileKind.Portrait or FileKind.LoreAttachment, DetectedFileType.Png) => Png,
        (FileKind.MapImage or FileKind.Portrait or FileKind.LoreAttachment, DetectedFileType.Jpeg) => Jpeg,
        (FileKind.MapImage or FileKind.Portrait or FileKind.LoreAttachment, DetectedFileType.WebP) => WebP,
        (FileKind.LoreAttachment or FileKind.LibraryDocument, DetectedFileType.Pdf) => Pdf,
        (FileKind.AppRelease, DetectedFileType.Zip) => Apk,
        _ => null,
    };

    public static string ExtensionFor(string contentType) => contentType switch
    {
        Png => ".png",
        Jpeg => ".jpg",
        WebP => ".webp",
        Pdf => ".pdf",
        Apk => ".apk",
        _ => string.Empty,
    };

    /// <summary>Message of the 400 for a file whose type is not accepted for the kind.</summary>
    public static string NotAllowedMessage(FileKind kind) => kind switch
    {
        FileKind.MapImage or FileKind.Portrait => "El fichero debe ser una imagen PNG, JPEG o WebP.",
        FileKind.LoreAttachment => "El fichero debe ser una imagen PNG, JPEG o WebP, o un PDF.",
        FileKind.LibraryDocument => "El fichero debe ser un PDF.",
        _ => "El fichero debe ser un APK.",
    };
}
