using OpenTrpg.Core.Application.Abstractions;
using OpenTrpg.Core.Application.Abstractions.Persistence;
using OpenTrpg.Core.Domain.Campaigns;

namespace OpenTrpg.Core.Application.Campaigns;

/// <summary>Campaign detail with members. Any member can read it; non-members get 404.</summary>
public sealed class GetCampaignHandler(ICampaignAccess access, ICampaignRepository campaigns)
{
    public async Task<CampaignDto> HandleAsync(Guid currentUserId, Guid campaignId, CancellationToken cancellationToken = default)
    {
        await access.RequireAsync(campaignId, currentUserId, CampaignRole.Player, cancellationToken);

        var campaign = await campaigns.GetByIdAsync(campaignId, cancellationToken) ?? throw CampaignErrors.CampaignNotFound();
        var members = await campaigns.ListMembersAsync(campaignId, cancellationToken);
        return CampaignDto.From(campaign, members, currentUserId);
    }
}
