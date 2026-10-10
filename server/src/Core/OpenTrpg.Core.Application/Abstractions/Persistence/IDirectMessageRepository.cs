using OpenTrpg.Core.Domain.Messages;

namespace OpenTrpg.Core.Application.Abstractions.Persistence;

/// <summary>A secret message with the names the API shows next to it.</summary>
public sealed record DirectMessageView(DirectMessage Message, string SenderDisplayName, string CharacterName);

public interface IDirectMessageRepository
{
    void Add(DirectMessage message);

    /// <summary>Tracked message, or null when it does not exist.</summary>
    Task<DirectMessage?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Read-only views of the given messages, newest first.</summary>
    Task<IReadOnlyList<DirectMessageView>> ListViewsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default);

    /// <summary>Messages received by the user in the campaign, newest first (at most <paramref name="limit"/>).</summary>
    Task<IReadOnlyList<DirectMessageView>> ListForRecipientAsync(Guid campaignId, Guid recipientUserId, bool unreadOnly, int limit, CancellationToken cancellationToken = default);

    /// <summary>Messages sent by the user in the campaign, newest first (at most <paramref name="limit"/>).</summary>
    Task<IReadOnlyList<DirectMessageView>> ListSentAsync(Guid campaignId, Guid senderUserId, bool unreadOnly, int limit, CancellationToken cancellationToken = default);

    Task<int> CountUnreadAsync(Guid campaignId, Guid recipientUserId, CancellationToken cancellationToken = default);
}
