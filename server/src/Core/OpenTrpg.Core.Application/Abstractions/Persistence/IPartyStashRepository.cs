using OpenTrpg.Core.Domain.Items;

namespace OpenTrpg.Core.Application.Abstractions.Persistence;

public interface IPartyStashRepository
{
    /// <summary>Tracked entries of the party stash of a campaign, oldest first.</summary>
    Task<IReadOnlyList<PartyStashItem>> ListByCampaignAsync(Guid campaignId, CancellationToken cancellationToken = default);

    /// <summary>Tracked entry, or null when it does not exist.</summary>
    Task<PartyStashItem?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    void Add(PartyStashItem item);

    void Remove(PartyStashItem item);
}
