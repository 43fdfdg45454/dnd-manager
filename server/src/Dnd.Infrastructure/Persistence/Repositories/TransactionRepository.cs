using Dnd.Application.Abstractions.Persistence;
using Dnd.Domain.Items;
using Microsoft.EntityFrameworkCore;

namespace Dnd.Infrastructure.Persistence.Repositories;

internal sealed class TransactionRepository(AppDbContext db) : ITransactionRepository
{
    public async Task<(IReadOnlyList<TransactionView> Items, int Total)> ListViewsAsync(TransactionQuery query, int skip, int take, CancellationToken cancellationToken = default)
    {
        var transactions = db.Transactions.AsNoTracking().Where(x => x.CampaignId == query.CampaignId);
        if (query.CharacterId is { } characterId)
        {
            transactions = transactions.Where(x => x.CharacterId == characterId);
        }

        var rows = Views(transactions);
        if (query.CharacterOwnerUserId is { } ownerId)
        {
            rows = rows.Where(r => r.OwnerUserId == ownerId);
        }

        // Sorted and paged in memory: SQLite cannot order by DateTimeOffset (same as change requests).
        var all = await rows.ToListAsync(cancellationToken);
        var page = all
            .OrderByDescending(r => r.Transaction.At)
            .ThenBy(r => r.Transaction.Id)
            .Skip(skip)
            .Take(take)
            .Select(r => r.ToView())
            .ToList();
        return (page, all.Count);
    }

    public async Task<TransactionView?> GetViewAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var row = await Views(db.Transactions.AsNoTracking().Where(x => x.Id == id)).FirstOrDefaultAsync(cancellationToken);
        return row?.ToView();
    }

    public void Add(Transaction transaction) => db.Transactions.Add(transaction);

    /// <summary>Shop, character and actor are optional (party stash operations, old records): left joins.</summary>
    private IQueryable<Row> Views(IQueryable<Transaction> transactions) =>
        from transaction in transactions
        from character in db.Characters.Where(c => c.Id == transaction.CharacterId).DefaultIfEmpty()
        from shop in db.Shops.Where(s => s.Id == transaction.ShopId).DefaultIfEmpty()
        from actor in db.Users.Where(u => u.Id == transaction.ActorUserId).DefaultIfEmpty()
        select new Row
        {
            Transaction = transaction,
            ShopName = shop == null ? null : shop.Name,
            CharacterName = character == null ? null : character.Name,
            OwnerUserId = character == null ? null : character.OwnerUserId,
            ActorDisplayName = actor == null ? null : actor.DisplayName,
        };

    /// <summary>Projection with settable members, so EF can still filter on them after the select.</summary>
    private sealed class Row
    {
        public required Transaction Transaction { get; init; }

        public string? ShopName { get; init; }

        public string? CharacterName { get; init; }

        public Guid? OwnerUserId { get; init; }

        public string? ActorDisplayName { get; init; }

        public TransactionView ToView() => new(Transaction, ShopName, CharacterName, ActorDisplayName);
    }
}
