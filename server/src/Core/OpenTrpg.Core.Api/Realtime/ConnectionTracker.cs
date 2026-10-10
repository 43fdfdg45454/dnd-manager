using OpenTrpg.Core.Application.Abstractions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;

namespace OpenTrpg.Core.Api.Realtime;

/// <summary>Realtime state of a user: open hub connections and the transport of the latest one.</summary>
/// <param name="Transport">"WebSockets", "ServerSentEvents" or "LongPolling"; null without connections.</param>
public sealed record RealtimeStatusDto(int Connections, string? Transport);

/// <summary>
/// Open connections of the campaign hub by user (singleton, single instance): enforces
/// <see cref="RealtimeOptions.MaxConnectionsPerUser"/>, remembers the campaign groups each connection
/// joined so a removed member can be taken out of them, and closes every connection of a deactivated
/// user. Never throws from <see cref="IRealtimeConnections"/>: failures are logged.
/// </summary>
public sealed class ConnectionTracker(
    IOptions<RealtimeOptions> options,
    IHubContext<CampaignHub> hub,
    ILogger<ConnectionTracker> logger) : IRealtimeConnections
{
    private readonly Lock _gate = new();
    private readonly Dictionary<Guid, Dictionary<string, TrackedConnection>> _byUser = [];
    private long _sequence;

    /// <summary>Registers a connection; false (and nothing registered) when the user already has the maximum.</summary>
    public bool TryRegister(Guid userId, HubCallerContext context, string? transport)
    {
        lock (_gate)
        {
            if (!_byUser.TryGetValue(userId, out var connections))
            {
                connections = [];
                _byUser[userId] = connections;
            }

            if (connections.Count >= options.Value.MaxConnectionsPerUser)
            {
                if (connections.Count == 0)
                {
                    _byUser.Remove(userId);
                }

                return false;
            }

            connections[context.ConnectionId] = new TrackedConnection(context, transport, ++_sequence);
            return true;
        }
    }

    public void Unregister(Guid userId, string connectionId)
    {
        lock (_gate)
        {
            if (_byUser.TryGetValue(userId, out var connections) && connections.Remove(connectionId) && connections.Count == 0)
            {
                _byUser.Remove(userId);
            }
        }
    }

    /// <summary>Remembers that the connection joined the campaign group (call before adding it to the group).</summary>
    public void MarkJoined(Guid userId, string connectionId, Guid campaignId)
    {
        lock (_gate)
        {
            if (_byUser.TryGetValue(userId, out var connections) && connections.TryGetValue(connectionId, out var connection))
            {
                connection.Campaigns.Add(campaignId);
            }
        }
    }

    public void MarkLeft(Guid userId, string connectionId, Guid campaignId)
    {
        lock (_gate)
        {
            if (_byUser.TryGetValue(userId, out var connections) && connections.TryGetValue(connectionId, out var connection))
            {
                connection.Campaigns.Remove(campaignId);
            }
        }
    }

    public RealtimeStatusDto GetStatus(Guid userId)
    {
        lock (_gate)
        {
            if (!_byUser.TryGetValue(userId, out var connections) || connections.Count == 0)
            {
                return new RealtimeStatusDto(0, null);
            }

            return new RealtimeStatusDto(connections.Count, connections.Values.MaxBy(c => c.Sequence)!.Transport);
        }
    }

    public async Task RemoveFromCampaignAsync(Guid userId, Guid campaignId, CancellationToken cancellationToken = default)
    {
        List<string> connectionIds;
        lock (_gate)
        {
            connectionIds = _byUser.TryGetValue(userId, out var connections)
                ? connections.Values.Where(c => c.Campaigns.Remove(campaignId)).Select(c => c.Context.ConnectionId).ToList()
                : [];
        }

        foreach (var connectionId in connectionIds)
        {
            try
            {
                await hub.Groups.RemoveFromGroupAsync(connectionId, CampaignGroups.Campaign(campaignId), cancellationToken);
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Could not remove the connection {ConnectionId} from the campaign {CampaignId}.", connectionId, campaignId);
            }
        }
    }

    public Task AbortAllAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        List<HubCallerContext> contexts;
        lock (_gate)
        {
            contexts = _byUser.TryGetValue(userId, out var connections) ? connections.Values.Select(c => c.Context).ToList() : [];
        }

        foreach (var context in contexts)
        {
            try
            {
                context.Abort();
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Could not close the connection {ConnectionId} of the user {UserId}.", context.ConnectionId, userId);
            }
        }

        return Task.CompletedTask;
    }

    private sealed class TrackedConnection(HubCallerContext context, string? transport, long sequence)
    {
        public HubCallerContext Context { get; } = context;

        public string? Transport { get; } = transport;

        /// <summary>Registration order: the latest connection gives the reported transport.</summary>
        public long Sequence { get; } = sequence;

        public HashSet<Guid> Campaigns { get; } = [];
    }
}
