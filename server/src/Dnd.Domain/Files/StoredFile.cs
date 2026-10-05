using Dnd.Domain.Common;

namespace Dnd.Domain.Files;

/// <summary>
/// Metadata of an uploaded file; the bytes live in the file storage under <see cref="StoragePath"/>
/// (relative to the storage root). A null <see cref="CampaignId"/> means a global (instance) file.
/// </summary>
public sealed class StoredFile : EntityBase
{
    public const int FileNameMaxLength = 255;
    public const int ContentTypeMaxLength = 100;
    public const int StoragePathMaxLength = 400;
    public const int Sha256Length = 64;

    private StoredFile()
    {
    }

    public Guid? CampaignId { get; private set; }

    /// <summary>Uploader; null for files registered by the system (e.g. the bundled SRD PDF).</summary>
    public Guid? OwnerUserId { get; private set; }

    public string FileName { get; private set; } = string.Empty;

    public string ContentType { get; private set; } = string.Empty;

    public long SizeBytes { get; private set; }

    /// <summary>Lowercase hexadecimal SHA-256 of the content.</summary>
    public string Sha256 { get; private set; } = string.Empty;

    public FileKind Kind { get; private set; }

    public string StoragePath { get; private set; } = string.Empty;

    /// <summary>Pixel size of images; null for other files.</summary>
    public int? WidthPx { get; private set; }

    public int? HeightPx { get; private set; }

    public bool IsImage => ContentType.StartsWith("image/", StringComparison.Ordinal);

    public static StoredFile Create(
        Guid id,
        Guid? campaignId,
        Guid? ownerUserId,
        string fileName,
        string contentType,
        long sizeBytes,
        string sha256,
        FileKind kind,
        string storagePath,
        int? widthPx,
        int? heightPx,
        DateTimeOffset now)
    {
        if (kind.BelongsToCampaign() != (campaignId is not null))
        {
            throw DomainException.RuleViolation("El ámbito del fichero (campaña o global) no corresponde a su tipo.");
        }

        return new StoredFile
        {
            Id = id,
            CampaignId = campaignId,
            OwnerUserId = ownerUserId,
            FileName = fileName,
            ContentType = contentType,
            SizeBytes = sizeBytes,
            Sha256 = sha256,
            Kind = kind,
            StoragePath = storagePath,
            WidthPx = widthPx,
            HeightPx = heightPx,
            CreatedAt = now,
        };
    }
}
