using OpenTrpg.Core.Application.Abstractions;
using OpenTrpg.Core.Application.Abstractions.Persistence;
using OpenTrpg.Core.Domain.Campaigns;

namespace OpenTrpg.Core.Application.Campaigns;

/// <summary>Removes another member: the owner removes anyone but themselves; a DM only players.</summary>
public sealed class RemoveMemberHandler(
    ICampaignAccess access,
    ICampaignRepository campaigns,
    IUnitOfWork unitOfWork,
    IRealtimeConnections realtime,
    ICampaignNotifier notifier,
    IDateTimeProvider clock)
{
    public async Task HandleAsync(Guid currentUserId, Guid campaignId, Guid userId, CancellationToken cancellationToken = default)
    {
        await access.RequireAsync(campaignId, currentUserId, CampaignRole.DM, cancellationToken);

        var campaign = await campaigns.GetWithMembersAsync(campaignId, cancellationToken) ?? throw CampaignErrors.CampaignNotFound();
        campaign.RemoveMember(currentUserId, userId);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        // Their open connections stop receiving the campaign's events; the app learns it was removed.
        await realtime.RemoveFromCampaignAsync(userId, campaignId, cancellationToken);
        await notifier.MembershipRemovedAsync(campaignId, userId, clock.UtcNow, cancellationToken);
    }
}
