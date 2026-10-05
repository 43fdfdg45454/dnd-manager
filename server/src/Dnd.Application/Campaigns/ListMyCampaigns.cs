using Dnd.Application.Abstractions.Persistence;

namespace Dnd.Application.Campaigns;

/// <summary>Campaigns where the current user is a member, with their own role, ordered by name.</summary>
public sealed class ListMyCampaignsHandler(ICampaignRepository campaigns)
{
    public Task<IReadOnlyList<CampaignSummaryDto>> HandleAsync(Guid currentUserId, CancellationToken cancellationToken = default) =>
        campaigns.ListSummariesForUserAsync(currentUserId, cancellationToken);
}
