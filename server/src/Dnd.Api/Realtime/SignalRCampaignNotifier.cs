using Dnd.Application.Abstractions;
using Microsoft.AspNetCore.SignalR;

namespace Dnd.Api.Realtime;

/// <summary>
/// <see cref="ICampaignNotifier"/> over <see cref="CampaignHub"/>. A failed send is logged and swallowed:
/// the change is already saved and clients catch up on their next fetch.
/// </summary>
internal sealed class SignalRCampaignNotifier(IHubContext<CampaignHub> hub, ILogger<SignalRCampaignNotifier> logger) : ICampaignNotifier
{
    public Task NotifyAsync(CampaignEvent e, CancellationToken cancellationToken = default) =>
        SendAsync(CampaignGroups.Campaign(e.CampaignId), e, cancellationToken);

    public Task NotifyUserAsync(Guid userId, CampaignEvent e, CancellationToken cancellationToken = default) =>
        SendAsync(CampaignGroups.User(userId), e, cancellationToken);

    private async Task SendAsync(string group, CampaignEvent e, CancellationToken cancellationToken)
    {
        try
        {
            await hub.Clients.Group(group).SendAsync(CampaignGroups.EventMethod, e, cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Could not send the realtime event {EventType} to {Group}.", e.Type, group);
        }
    }
}
