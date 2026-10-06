using Dnd.Application.Abstractions;
using Dnd.Application.Abstractions.Persistence;
using Dnd.Domain.Campaigns;

namespace Dnd.Application.Campaigns;

/// <summary>The current user leaves the campaign. The owner must transfer the ownership first.</summary>
public sealed class LeaveCampaignHandler(
    ICampaignAccess access,
    ICampaignRepository campaigns,
    IUnitOfWork unitOfWork,
    IRealtimeConnections realtime,
    ICampaignNotifier notifier,
    IDateTimeProvider clock)
{
    public async Task HandleAsync(Guid currentUserId, Guid campaignId, CancellationToken cancellationToken = default)
    {
        await access.RequireAsync(campaignId, currentUserId, CampaignRole.Player, cancellationToken);

        var campaign = await campaigns.GetWithMembersAsync(campaignId, cancellationToken) ?? throw CampaignErrors.CampaignNotFound();
        campaign.Leave(currentUserId);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        // Their open connections stop receiving the campaign's events; the app learns it was removed.
        await realtime.RemoveFromCampaignAsync(currentUserId, campaignId, cancellationToken);
        await notifier.MembershipRemovedAsync(campaignId, currentUserId, clock.UtcNow, cancellationToken);
    }
}
