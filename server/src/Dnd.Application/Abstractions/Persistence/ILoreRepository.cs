using Dnd.Domain.Common;
using Dnd.Domain.Lore;

namespace Dnd.Application.Abstractions.Persistence;

/// <param name="PlayersOnly">Only entries visible to players.</param>
/// <param name="Search">Lowercase text looked up in titles and content.</param>
public sealed record LoreFilter(bool PlayersOnly, LoreCategory? Category = null, string? Search = null, Guid? ParentId = null);

public interface ILoreRepository
{
    /// <summary>Read-only entries of the campaign (without attachments), ordered by sort order and title.</summary>
    Task<IReadOnlyList<LoreEntry>> ListByCampaignAsync(Guid campaignId, LoreFilter filter, CancellationToken cancellationToken = default);

    /// <summary>Tracked entry with its attachments.</summary>
    Task<LoreEntry?> GetWithAttachmentsAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Read-only entry of the campaign by id (no attachments), or null.</summary>
    Task<LoreEntry?> FindInCampaignAsync(Guid campaignId, Guid id, CancellationToken cancellationToken = default);

    Task<bool> SlugExistsAsync(Guid campaignId, string slug, CancellationToken cancellationToken = default);

    /// <summary>(Id, ParentId) of every entry of the campaign, to detect cycles when moving an entry.</summary>
    Task<IReadOnlyDictionary<Guid, Guid?>> ListParentLinksAsync(Guid campaignId, CancellationToken cancellationToken = default);

    void Add(LoreEntry entry);

    /// <summary>Deletes the entry and its attachments; its children become root entries (done by the database).</summary>
    void Remove(LoreEntry entry);
}
