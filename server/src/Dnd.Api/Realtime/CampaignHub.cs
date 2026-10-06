using Dnd.Application.Abstractions;
using Dnd.Application.Abstractions.Persistence;
using Dnd.Infrastructure.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Connections.Features;
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
/// <c>access_token</c> query parameter is accepted on this path). Inactive users are refused and each
/// user may keep at most <see cref="RealtimeOptions.MaxConnectionsPerUser"/> connections
/// (<see cref="ConnectionTracker"/>).
/// </summary>
[Authorize]
public sealed class CampaignHub(ICampaignAccess access, IUserRepository users, ConnectionTracker tracker) : Hub
{
    public const string Path = "/hubs/campaign";

    public const string TooManyConnectionsMessage = "Demasiadas conexiones abiertas.";

    public const string InactiveAccountMessage = "La cuenta está desactivada.";

    public override async Task OnConnectedAsync()
    {
        var userId = CurrentUserId();
        if (await users.GetByIdAsync(userId, Context.ConnectionAborted) is not { IsActive: true })
        {
            throw new HubException(InactiveAccountMessage);
        }

        var transport = Context.Features.Get<IHttpTransportFeature>()?.TransportType.ToString();
        if (!tracker.TryRegister(userId, Context, transport))
        {
            throw new HubException(TooManyConnectionsMessage);
        }

        try
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, CampaignGroups.User(userId));
            await base.OnConnectedAsync();
        }
        catch
        {
            // OnDisconnectedAsync is not called when OnConnectedAsync fails: release the slot here.
            tracker.Unregister(userId, Context.ConnectionId);
            throw;
        }
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        if (Context.User is { } user && Guid.TryParse(user.FindFirst(JwtClaimTypes.Subject)?.Value, out var userId))
        {
            tracker.Unregister(userId, Context.ConnectionId);
        }

        return base.OnDisconnectedAsync(exception);
    }

    /// <summary>Starts receiving the events of a campaign the user is a member of.</summary>
    public async Task JoinCampaign(Guid campaignId)
    {
        var userId = CurrentUserId();
        await EnsureMemberAsync(campaignId, userId);

        // Marked before joining so a concurrent removal finds the connection; checked again afterwards
        // in case the membership ended in between.
        tracker.MarkJoined(userId, Context.ConnectionId, campaignId);
        await Groups.AddToGroupAsync(Context.ConnectionId, CampaignGroups.Campaign(campaignId), Context.ConnectionAborted);
        try
        {
            await EnsureMemberAsync(campaignId, userId);
        }
        catch (HubException)
        {
            await LeaveCampaign(campaignId);
            throw;
        }
    }

    /// <summary>Stops receiving the events of a campaign.</summary>
    public Task LeaveCampaign(Guid campaignId)
    {
        tracker.MarkLeft(CurrentUserId(), Context.ConnectionId, campaignId);
        return Groups.RemoveFromGroupAsync(Context.ConnectionId, CampaignGroups.Campaign(campaignId), Context.ConnectionAborted);
    }

    private async Task EnsureMemberAsync(Guid campaignId, Guid userId)
    {
        if (await access.GetRoleAsync(campaignId, userId, Context.ConnectionAborted) is null)
        {
            throw new HubException("No perteneces a esta campaña.");
        }
    }

    private Guid CurrentUserId() =>
        Context.User is { } user && Guid.TryParse(user.FindFirst(JwtClaimTypes.Subject)?.Value, out var id)
            ? id
            : throw new HubException("La sesión no es válida.");
}
