namespace Dnd.Application.Abstractions;

/// <summary>
/// Control over the open realtime connections of a user (ADR 0006). Implementations never throw: the
/// change that triggered the call is already saved.
/// </summary>
public interface IRealtimeConnections
{
    /// <summary>Stops the user's connections from receiving the events of the campaign (removed member, left).</summary>
    Task RemoveFromCampaignAsync(Guid userId, Guid campaignId, CancellationToken cancellationToken = default);

    /// <summary>Closes every connection of the user (deactivated account).</summary>
    Task AbortAllAsync(Guid userId, CancellationToken cancellationToken = default);
}

/// <summary>Does nothing: the default outside the API host (no realtime transport).</summary>
public sealed class NoopRealtimeConnections : IRealtimeConnections
{
    public Task RemoveFromCampaignAsync(Guid userId, Guid campaignId, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task AbortAllAsync(Guid userId, CancellationToken cancellationToken = default) => Task.CompletedTask;
}
