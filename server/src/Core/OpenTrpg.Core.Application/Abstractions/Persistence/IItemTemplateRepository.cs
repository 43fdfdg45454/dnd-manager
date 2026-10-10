using OpenTrpg.Core.Domain.Catalog;
using OpenTrpg.Core.Application.Items;

namespace OpenTrpg.Core.Application.Abstractions.Persistence;

/// <summary>Which item templates a campaign search returns.</summary>
public enum ItemSource
{
    All,
    Srd,
    Homebrew,
}

/// <summary>Item templates as seen from a campaign: SRD items plus the campaign's homebrew.</summary>
public interface IItemTemplateRepository
{
    /// <summary>Untracked SRD and/or homebrew items of the campaign matching the filter, ordered by name.</summary>
    Task<(IReadOnlyList<ItemTemplate> Items, int Total)> SearchAsync(
        Guid campaignId,
        ItemFilter filter,
        ItemSource source,
        int skip,
        int take,
        CancellationToken cancellationToken = default);

    /// <summary>Untracked item usable in the campaign (SRD, or homebrew of that campaign); null otherwise.</summary>
    Task<ItemTemplate?> GetVisibleAsync(Guid campaignId, Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// A template the campaign can add to a shop, an inventory or the stash: its homebrew, the base content and the
    /// content packs the campaign enables (<c>CampaignContentPacks</c>); null otherwise.
    /// </summary>
    Task<ItemTemplate?> GetSelectableAsync(Guid campaignId, Guid id, CancellationToken cancellationToken = default);

    /// <summary>Tracked homebrew item of the campaign; null for SRD items or other campaigns.</summary>
    Task<ItemTemplate?> GetHomebrewAsync(Guid campaignId, Guid id, CancellationToken cancellationToken = default);

    /// <summary>Untracked templates by id (unknown ids are left out).</summary>
    Task<IReadOnlyList<ItemTemplate>> ListByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default);

    /// <summary>True when an inventory entry or a shop item references the template.</summary>
    Task<bool> IsInUseAsync(Guid id, CancellationToken cancellationToken = default);

    void Add(ItemTemplate template);

    void Remove(ItemTemplate template);
}
