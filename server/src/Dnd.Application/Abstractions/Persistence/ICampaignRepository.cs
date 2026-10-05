using Dnd.Application.Campaigns;
using Dnd.Domain.Campaigns;

namespace Dnd.Application.Abstractions.Persistence;

public interface ICampaignRepository
{
    /// <summary>Tracked campaign with its members loaded, ready to be modified.</summary>
    Task<Campaign?> GetWithMembersAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Read-only campaign without members.</summary>
    Task<Campaign?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Campaigns where the user is a member, ordered by name.</summary>
    Task<IReadOnlyList<CampaignSummaryDto>> ListSummariesForUserAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Members with their user data, ordered by role (Owner, DM, Player) and display name.</summary>
    Task<IReadOnlyList<MemberDto>> ListMembersAsync(Guid campaignId, CancellationToken cancellationToken = default);

    void Add(Campaign campaign);

    /// <summary>Deletes the campaign; members and ownership transfers are deleted in cascade.</summary>
    void Remove(Campaign campaign);

    void AddOwnershipTransfer(OwnershipTransfer transfer);
}
