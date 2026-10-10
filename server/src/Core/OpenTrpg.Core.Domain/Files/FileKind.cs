namespace OpenTrpg.Core.Domain.Files;

/// <summary>What an uploaded file is for; decides who can upload it and which types are accepted.</summary>
public enum FileKind
{
    MapImage,
    Portrait,
    LoreAttachment,
    LibraryDocument,
    AppRelease,
}

public static class FileKindExtensions
{
    /// <summary>Files that belong to a campaign (the others are global to the instance).</summary>
    public static bool BelongsToCampaign(this FileKind kind) => kind is FileKind.MapImage or FileKind.Portrait or FileKind.LoreAttachment;
}
