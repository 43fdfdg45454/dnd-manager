using OpenTrpg.Core.Domain.Common;

namespace OpenTrpg.Core.Domain.Library;

/// <summary>A library document recommended by the DMs of a campaign (one entry per campaign and document).</summary>
public sealed class CampaignDocument : EntityBase
{
    public const int NoteMaxLength = 500;

    private CampaignDocument()
    {
    }

    public Guid CampaignId { get; private set; }

    public Guid DocumentId { get; private set; }

    public string? Note { get; private set; }

    public static CampaignDocument Create(Guid campaignId, Guid documentId, string? note, DateTimeOffset now)
    {
        var document = new CampaignDocument { CampaignId = campaignId, DocumentId = documentId, CreatedAt = now };
        document.SetNote(note);
        return document;
    }

    public void SetNote(string? note)
    {
        var trimmed = note?.Trim();
        if (trimmed is { Length: > NoteMaxLength })
        {
            throw DomainException.RuleViolation($"La nota no puede superar los {NoteMaxLength} caracteres.");
        }

        Note = string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}
