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

        var rows = from transaction in transactions
                   join character in db.Characters on transaction.CharacterId equals character.Id
                   join shop in db.Shops on transaction.ShopId equals shop.Id
                   select new { Transaction = transaction, CharacterName = character.Name, character.OwnerUserId, ShopName = shop.Name };
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
            .Select(r => new TransactionView(r.Transaction, r.ShopName, r.CharacterName))
            .ToList();
        return (page, all.Count);
    }

    public void Add(Transaction transaction) => db.Transactions.Add(transaction);
}
