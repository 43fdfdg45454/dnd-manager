using OpenTrpg.Core.Application.Abstractions;
using OpenTrpg.Core.Application.Abstractions.Persistence;
using OpenTrpg.Core.Domain.Campaigns;

namespace OpenTrpg.Core.Application.Campaigns;

/// <summary>Members of a campaign. Any member can list them.</summary>
public sealed class ListMembersHandler(ICampaignAccess access, ICampaignRepository campaigns)
{
    public async Task<IReadOnlyList<MemberDto>> HandleAsync(Guid currentUserId, Guid campaignId, CancellationToken cancellationToken = default)
    {
        await access.RequireAsync(campaignId, currentUserId, CampaignRole.Player, cancellationToken);
        return await campaigns.ListMembersAsync(campaignId, cancellationToken);
    }
}
