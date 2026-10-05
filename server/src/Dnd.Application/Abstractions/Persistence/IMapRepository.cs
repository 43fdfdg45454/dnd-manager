using Dnd.Domain.Maps;

namespace Dnd.Application.Abstractions.Persistence;

public interface IMapRepository
{
    /// <summary>Read-only maps of the campaign (without pins), by sort order and name; only visible-to-players ones when <paramref name="playersOnly"/>.</summary>
    Task<IReadOnlyList<Map>> ListByCampaignAsync(Guid campaignId, bool playersOnly, CancellationToken cancellationToken = default);

    /// <summary>Tracked map with its pins.</summary>
    Task<Map?> GetWithPinsAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Highest sort order of the campaign's maps, or -1 when there are none.</summary>
    Task<int> MaxSortOrderAsync(Guid campaignId, CancellationToken cancellationToken = default);

    void Add(Map map);

    /// <summary>Deletes the map; its pins go with it.</summary>
    void Remove(Map map);
}
