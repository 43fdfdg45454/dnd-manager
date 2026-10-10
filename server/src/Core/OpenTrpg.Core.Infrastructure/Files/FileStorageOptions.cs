namespace OpenTrpg.Core.Infrastructure.Files;

public sealed class FileStorageOptions
{
    public const string SectionName = "FileStorage";

    /// <summary>Root directory for uploaded files (maps, portraits, library PDFs, APK releases).</summary>
    public string RootPath { get; set; } = "data/files";

    /// <summary>Maximum upload size in megabytes. Keep in sync with nginx client_max_body_size.</summary>
    public int MaxUploadMegabytes { get; set; } = 200;
}
