using Dnd.Domain.Common;

namespace Dnd.Domain.Lore;

/// <summary>A file (image or PDF of the campaign) attached to a lore entry.</summary>
public sealed class LoreAttachment : EntityBase
{
    public const int CaptionMaxLength = 200;

    private LoreAttachment()
    {
    }

    public Guid LoreEntryId { get; private set; }

    public Guid FileId { get; private set; }

    public string? Caption { get; private set; }

    internal static LoreAttachment Create(Guid loreEntryId, Guid fileId, string? caption, DateTimeOffset now)
    {
        var trimmed = caption?.Trim();
        if (trimmed is { Length: > CaptionMaxLength })
        {
            throw DomainException.RuleViolation($"El pie de foto no puede superar los {CaptionMaxLength} caracteres.");
        }

        return new LoreAttachment
        {
            LoreEntryId = loreEntryId,
            FileId = fileId,
            Caption = string.IsNullOrEmpty(trimmed) ? null : trimmed,
            CreatedAt = now,
        };
    }
}
