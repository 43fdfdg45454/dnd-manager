using Dnd.Application.Files;
using Dnd.Domain.Files;
using Dnd.Domain.Lore;

namespace Dnd.Application.Lore;

/// <summary>An entry of the flat tree: the client builds the hierarchy with <see cref="ParentId"/>.</summary>
public sealed record LoreSummaryDto(
    Guid Id,
    Guid CampaignId,
    string Title,
    string Slug,
    string Category,
    string Visibility,
    Guid? ParentId,
    int SortOrder,
    Guid? CoverFileId,
    string? CoverUrl,
    DateTimeOffset UpdatedAt)
{
    public static LoreSummaryDto From(LoreEntry entry) => new(
        entry.Id,
        entry.CampaignId,
        entry.Title,
        entry.Slug,
        entry.Category.ToString(),
        entry.Visibility.ToString(),
        entry.ParentId,
        entry.SortOrder,
        entry.CoverFileId,
        FileUrls.For(entry.CoverFileId),
        entry.UpdatedAt);
}

public sealed record LoreAttachmentDto(Guid Id, Guid FileId, string FileName, string ContentType, long SizeBytes, string Url, string? Caption)
{
    public static LoreAttachmentDto From(LoreAttachment attachment, StoredFile file) =>
        new(attachment.Id, file.Id, file.FileName, file.ContentType, file.SizeBytes, FileUrls.For(file.Id), attachment.Caption);
}

public sealed record LoreEntryDto(
    Guid Id,
    Guid CampaignId,
    string Title,
    string Slug,
    string Category,
    string ContentMarkdown,
    string Visibility,
    Guid? ParentId,
    int SortOrder,
    Guid? CoverFileId,
    string? CoverUrl,
    Guid CreatedByUserId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<LoreAttachmentDto> Attachments);
