using Dnd.Domain.Items;

namespace Dnd.Application.Abstractions.Persistence;

public interface IShopRepository
{
    /// <summary>Untracked shops of the campaign with their items, ordered by name; only open ones when <paramref name="openOnly"/>.</summary>
    Task<IReadOnlyList<Shop>> ListByCampaignAsync(Guid campaignId, bool openOnly, CancellationToken cancellationToken = default);

    /// <summary>Tracked shop with its items, ready to be modified.</summary>
    Task<Shop?> GetWithItemsAsync(Guid id, CancellationToken cancellationToken = default);

    void Add(Shop shop);

    /// <summary>Deletes the shop; its items and transactions are deleted in cascade.</summary>
    void Remove(Shop shop);
}
