using OpenTrpg.Core.Domain.Common;

namespace OpenTrpg.Core.Domain.Lore;

/// <summary>
/// A lore page of a campaign (world, place, NPC, quest...). Entries form a tree through
/// <see cref="ParentId"/>; <see cref="Slug"/> is unique inside the campaign and is generated once
/// at creation so <c>[[slug]]</c> links keep working when the title changes.
/// </summary>
public sealed class LoreEntry : EntityBase
{
    public const int TitleMaxLength = 200;
    public const int ContentMaxLength = 100_000;
    public const int MaxSortOrder = 100_000;

    private readonly List<LoreAttachment> _attachments = [];

    private LoreEntry()
    {
    }

    public Guid CampaignId { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public string Slug { get; private set; } = string.Empty;

    public LoreCategory Category { get; private set; }

    public string ContentMarkdown { get; private set; } = string.Empty;

    public ContentVisibility Visibility { get; private set; }

    public Guid? ParentId { get; private set; }

    public int SortOrder { get; private set; }

    public Guid? CoverFileId { get; private set; }

    public Guid CreatedByUserId { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public IReadOnlyCollection<LoreAttachment> Attachments => _attachments;

    public static LoreEntry Create(
        Guid campaignId,
        string title,
        string slug,
        LoreCategory category,
        string? contentMarkdown,
        ContentVisibility visibility,
        Guid? parentId,
        Guid? coverFileId,
        Guid createdByUserId,
        DateTimeOffset now) => new()
    {
        CampaignId = campaignId,
        Title = NormalizeTitle(title),
        Slug = slug,
        Category = category,
        ContentMarkdown = NormalizeContent(contentMarkdown),
        Visibility = visibility,
        ParentId = parentId,
        CoverFileId = coverFileId,
        CreatedByUserId = createdByUserId,
        CreatedAt = now,
        UpdatedAt = now,
    };

    /// <summary>Null arguments keep the current value. The slug never changes.</summary>
    public void Update(string? title, LoreCategory? category, string? contentMarkdown, ContentVisibility? visibility, int? sortOrder, DateTimeOffset now)
    {
        var newTitle = title is null ? Title : NormalizeTitle(title);
        var newContent = contentMarkdown is null ? ContentMarkdown : NormalizeContent(contentMarkdown);
        var newSort = sortOrder is null ? SortOrder : ValidateSortOrder(sortOrder.Value);

        Title = newTitle;
        ContentMarkdown = newContent;
        SortOrder = newSort;
        Category = category ?? Category;
        Visibility = visibility ?? Visibility;
        UpdatedAt = now;
    }

    public void SetParent(Guid? parentId, DateTimeOffset now)
    {
        if (parentId == Id)
        {
            throw DomainException.RuleViolation("Una entrada no puede ser su propio padre.");
        }

        ParentId = parentId;
        UpdatedAt = now;
    }

    public void SetCover(Guid? fileId, DateTimeOffset now)
    {
        CoverFileId = fileId;
        UpdatedAt = now;
    }

    public LoreAttachment AddAttachment(Guid fileId, string? caption, DateTimeOffset now)
    {
        var attachment = LoreAttachment.Create(Id, fileId, caption, now);
        _attachments.Add(attachment);
        UpdatedAt = now;
        return attachment;
    }

    public LoreAttachment FindAttachment(Guid attachmentId) =>
        _attachments.FirstOrDefault(a => a.Id == attachmentId) ?? throw DomainException.NotFound("Adjunto no encontrado.");

    public void RemoveAttachment(Guid attachmentId, DateTimeOffset now)
    {
        _attachments.Remove(FindAttachment(attachmentId));
        UpdatedAt = now;
    }

    private static string NormalizeTitle(string title)
    {
        var trimmed = (title ?? string.Empty).Trim();
        return trimmed.Length is 0 or > TitleMaxLength
            ? throw DomainException.RuleViolation($"El título debe tener entre 1 y {TitleMaxLength} caracteres.")
            : trimmed;
    }

    private static string NormalizeContent(string? content)
    {
        var value = content ?? string.Empty;
        return value.Length > ContentMaxLength
            ? throw DomainException.RuleViolation($"El contenido no puede superar los {ContentMaxLength} caracteres.")
            : value;
    }

    private static int ValidateSortOrder(int sortOrder) => sortOrder is >= 0 and <= MaxSortOrder
        ? sortOrder
        : throw DomainException.RuleViolation($"El orden debe estar entre 0 y {MaxSortOrder}.");
}
