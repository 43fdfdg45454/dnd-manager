using Dnd.Application.Abstractions;
using Dnd.Application.Abstractions.Persistence;
using Dnd.Domain.Campaigns;

namespace Dnd.Application.Campaigns;

/// <summary>Removes another member: the owner removes anyone but themselves; a DM only players.</summary>
public sealed class RemoveMemberHandler(ICampaignAccess access, ICampaignRepository campaigns, IUnitOfWork unitOfWork)
{
    public async Task HandleAsync(Guid currentUserId, Guid campaignId, Guid userId, CancellationToken cancellationToken = default)
    {
        await access.RequireAsync(campaignId, currentUserId, CampaignRole.DM, cancellationToken);

        var campaign = await campaigns.GetWithMembersAsync(campaignId, cancellationToken) ?? throw CampaignErrors.CampaignNotFound();
        campaign.RemoveMember(currentUserId, userId);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
