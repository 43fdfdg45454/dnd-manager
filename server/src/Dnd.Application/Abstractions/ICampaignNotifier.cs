using Dnd.Domain.Sessions;

namespace Dnd.Application.Abstractions;

/// <summary>
/// Realtime event of a campaign (SignalR message <c>campaignEvent</c>). It only carries ids: the client
/// fetches again over HTTP whatever it is allowed to see.
/// </summary>
/// <param name="Type">One of <see cref="CampaignEventTypes"/>.</param>
/// <param name="CharacterId">Character concerned, when there is one.</param>
/// <param name="EntityId">Other entity concerned (message, shop, change request, session...), or null.</param>
public sealed record CampaignEvent(string Type, Guid CampaignId, Guid? CharacterId, Guid? EntityId, DateTimeOffset At);

/// <summary>Values of <see cref="CampaignEvent.Type"/>.</summary>
public static class CampaignEventTypes
{
    /// <summary>A secret message for the user (sent to that user only).</summary>
    public const string MessageReceived = "message.received";

    /// <summary>Combat state, rest, sheet, inventory or an approved request of a character changed.</summary>
    public const string CharacterUpdated = "character.updated";

    /// <summary>The DM forced a rest on the party (<see cref="CampaignEvent.EntityId"/> is null).</summary>
    public const string PartyRest = "party.rest";

    public const string ShopUpdated = "shop.updated";

    public const string ChangeRequestUpdated = "changeRequest.updated";

    /// <summary>A DM approved or rejected the user's request (sent to the requester only; <see cref="CampaignEvent.EntityId"/> = request).</summary>
    public const string ChangeRequestResolved = "changeRequest.resolved";

    public const string SessionUpdated = "session.updated";

    public const string PartyStashUpdated = "party.stash.updated";

    /// <summary>
    /// The user was removed from the campaign or left it (sent to that user only; their connections no
    /// longer receive the campaign's events).
    /// </summary>
    public const string MembershipRemoved = "membership.removed";

    /// <summary>A rest request was created, approved, rejected or cancelled (<see cref="CampaignEvent.EntityId"/> = request).</summary>
    public const string RestRequestUpdated = "restRequest.updated";

    /// <summary>A DM granted a level-up to the character (sent to the campaign and to the character's owner).</summary>
    public const string LevelUpGranted = "levelUp.granted";

    /// <summary>The character completed a level-up (sent to the campaign, together with <see cref="CharacterUpdated"/>).</summary>
    public const string LevelUpCompleted = "levelUp.completed";

    /// <summary>
    /// The user was invited to the campaign (sent to that user only; <see cref="CampaignEvent.EntityId"/> = invitation).
    /// The members of the campaign receive <see cref="MembersUpdated"/> when the invitation is accepted or declined.
    /// </summary>
    public const string InvitationReceived = "invitation.received";

    /// <summary>Someone accepted or declined an invitation, or an invitation was cancelled.</summary>
    public const string MembersUpdated = "members.updated";
}

/// <summary>
/// Publishes realtime campaign events. Called after <c>SaveChanges</c>; implementations never throw
/// (a failed notification is logged and the request still succeeds).
/// </summary>
public interface ICampaignNotifier
{
    /// <summary>Sends the event to every connection that joined the campaign.</summary>
    Task NotifyAsync(CampaignEvent e, CancellationToken cancellationToken = default);

    /// <summary>Sends the event only to the connections of one user (e.g. <see cref="CampaignEventTypes.MessageReceived"/>).</summary>
    Task NotifyUserAsync(Guid userId, CampaignEvent e, CancellationToken cancellationToken = default);
}

/// <summary>Notifier that does nothing: the default outside the API host (no realtime transport).</summary>
public sealed class NoopCampaignNotifier : ICampaignNotifier
{
    public Task NotifyAsync(CampaignEvent e, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task NotifyUserAsync(Guid userId, CampaignEvent e, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

/// <summary>Shortcuts to build and publish the usual events.</summary>
public static class CampaignNotifierExtensions
{
    public static Task CharacterUpdatedAsync(this ICampaignNotifier notifier, Guid campaignId, Guid characterId, DateTimeOffset at, CancellationToken cancellationToken = default) =>
        notifier.NotifyAsync(new CampaignEvent(CampaignEventTypes.CharacterUpdated, campaignId, characterId, null, at), cancellationToken);

    public static Task ChangeRequestUpdatedAsync(this ICampaignNotifier notifier, Guid campaignId, Guid characterId, Guid requestId, DateTimeOffset at, CancellationToken cancellationToken = default) =>
        notifier.NotifyAsync(new CampaignEvent(CampaignEventTypes.ChangeRequestUpdated, campaignId, characterId, requestId, at), cancellationToken);

    public static Task ChangeRequestResolvedAsync(this ICampaignNotifier notifier, Guid requesterUserId, Guid campaignId, Guid characterId, Guid requestId, DateTimeOffset at, CancellationToken cancellationToken = default) =>
        notifier.NotifyUserAsync(requesterUserId, new CampaignEvent(CampaignEventTypes.ChangeRequestResolved, campaignId, characterId, requestId, at), cancellationToken);

    public static Task SessionUpdatedAsync(this ICampaignNotifier notifier, GameSession session, DateTimeOffset at, CancellationToken cancellationToken = default) =>
        notifier.NotifyAsync(new CampaignEvent(CampaignEventTypes.SessionUpdated, session.CampaignId, null, session.Id, at), cancellationToken);

    public static Task RestRequestUpdatedAsync(this ICampaignNotifier notifier, Guid campaignId, Guid characterId, Guid requestId, DateTimeOffset at, CancellationToken cancellationToken = default) =>
        notifier.NotifyAsync(new CampaignEvent(CampaignEventTypes.RestRequestUpdated, campaignId, characterId, requestId, at), cancellationToken);

    public static Task MembershipRemovedAsync(this ICampaignNotifier notifier, Guid campaignId, Guid userId, DateTimeOffset at, CancellationToken cancellationToken = default) =>
        notifier.NotifyUserAsync(userId, new CampaignEvent(CampaignEventTypes.MembershipRemoved, campaignId, null, null, at), cancellationToken);

    public static Task InvitationReceivedAsync(this ICampaignNotifier notifier, Guid campaignId, Guid userId, Guid invitationId, DateTimeOffset at, CancellationToken cancellationToken = default) =>
        notifier.NotifyUserAsync(userId, new CampaignEvent(CampaignEventTypes.InvitationReceived, campaignId, null, invitationId, at), cancellationToken);

    public static Task MembersUpdatedAsync(this ICampaignNotifier notifier, Guid campaignId, DateTimeOffset at, CancellationToken cancellationToken = default) =>
        notifier.NotifyAsync(new CampaignEvent(CampaignEventTypes.MembersUpdated, campaignId, null, null, at), cancellationToken);

    public static Task StashUpdatedAsync(this ICampaignNotifier notifier, Guid campaignId, DateTimeOffset at, CancellationToken cancellationToken = default) =>
        notifier.NotifyAsync(new CampaignEvent(CampaignEventTypes.PartyStashUpdated, campaignId, null, null, at), cancellationToken);
}
