using Dnd.Api.Auth;
using Dnd.Application.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Dnd.Api.Realtime;

/// <summary>SignalR group names and the client method that receives the events.</summary>
public static class CampaignGroups
{
    /// <summary>Client method invoked with a <see cref="CampaignEvent"/>.</summary>
    public const string EventMethod = "campaignEvent";

    public static string Campaign(Guid campaignId) => $"campaign:{campaignId}";

    public static string User(Guid userId) => $"user:{userId}";
}

/// <summary>
/// Realtime hub (<c>/hubs/campaign</c>). It only notifies (see <see cref="ICampaignNotifier"/>): every
/// connection joins <c>user:{id}</c> on connect and <c>campaign:{id}</c> through
/// <see cref="JoinCampaign"/> once membership is checked. Authenticated with the API's JWT (the
/// <c>access_token</c> query parameter is accepted on this path).
/// </summary>
[Authorize]
public sealed class CampaignHub(ICampaignAccess access) : Hub
{
    public const string Path = "/hubs/campaign";

    public override async Task OnConnectedAsync()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, CampaignGroups.User(CurrentUserId()));
        await base.OnConnectedAsync();
    }

    /// <summary>Starts receiving the events of a campaign the user is a member of.</summary>
    public async Task JoinCampaign(Guid campaignId)
    {
        if (await access.GetRoleAsync(campaignId, CurrentUserId(), Context.ConnectionAborted) is null)
        {
            throw new HubException("No perteneces a esta campaña.");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, CampaignGroups.Campaign(campaignId), Context.ConnectionAborted);
    }

    /// <summary>Stops receiving the events of a campaign.</summary>
    public Task LeaveCampaign(Guid campaignId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, CampaignGroups.Campaign(campaignId), Context.ConnectionAborted);

    private Guid CurrentUserId() =>
        Context.User is { } user ? user.GetUserId() : throw new HubException("La sesión no es válida.");
}
