using Dnd.Application.Abstractions.Persistence;
using Dnd.Domain.Messages;
using Microsoft.EntityFrameworkCore;

namespace Dnd.Infrastructure.Persistence.Repositories;

internal sealed class DirectMessageRepository(AppDbContext db) : IDirectMessageRepository
{
    public void Add(DirectMessage message) => db.DirectMessages.Add(message);

    public Task<DirectMessage?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        db.DirectMessages.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<IReadOnlyList<DirectMessageView>> ListViewsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default) =>
        ids.Count == 0
            ? Task.FromResult<IReadOnlyList<DirectMessageView>>([])
            : ViewsAsync(db.DirectMessages.Where(x => ids.Contains(x.Id)), int.MaxValue, cancellationToken);

    public Task<IReadOnlyList<DirectMessageView>> ListForRecipientAsync(Guid campaignId, Guid recipientUserId, bool unreadOnly, int limit, CancellationToken cancellationToken = default)
    {
        var messages = db.DirectMessages.Where(x => x.CampaignId == campaignId && x.RecipientUserId == recipientUserId);
        return ViewsAsync(unreadOnly ? messages.Where(x => x.ReadAt == null) : messages, limit, cancellationToken);
    }

    public Task<IReadOnlyList<DirectMessageView>> ListSentAsync(Guid campaignId, Guid senderUserId, bool unreadOnly, int limit, CancellationToken cancellationToken = default)
    {
        var messages = db.DirectMessages.Where(x => x.CampaignId == campaignId && x.SenderUserId == senderUserId);
        return ViewsAsync(unreadOnly ? messages.Where(x => x.ReadAt == null) : messages, limit, cancellationToken);
    }

    public Task<int> CountUnreadAsync(Guid campaignId, Guid recipientUserId, CancellationToken cancellationToken = default) =>
        db.DirectMessages.CountAsync(x => x.CampaignId == campaignId && x.RecipientUserId == recipientUserId && x.ReadAt == null, cancellationToken);

    private async Task<IReadOnlyList<DirectMessageView>> ViewsAsync(IQueryable<DirectMessage> messages, int limit, CancellationToken cancellationToken)
    {
        var rows = await (
                from message in messages.AsNoTracking()
                join sender in db.Users on message.SenderUserId equals sender.Id
                join character in db.Characters on message.CharacterId equals character.Id
                select new { Message = message, SenderDisplayName = sender.DisplayName, CharacterName = character.Name })
            .ToListAsync(cancellationToken);

        // Sorted and limited in memory: SQLite cannot order by DateTimeOffset.
        return rows
            .OrderByDescending(r => r.Message.SentAt)
            .ThenBy(r => r.Message.Id)
            .Take(limit)
            .Select(r => new DirectMessageView(r.Message, r.SenderDisplayName, r.CharacterName))
            .ToList();
    }
}
