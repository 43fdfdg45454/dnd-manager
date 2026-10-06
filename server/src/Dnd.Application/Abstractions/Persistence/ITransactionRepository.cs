using Dnd.Domain.Items;

namespace Dnd.Application.Abstractions.Persistence;

/// <summary>Filter of transaction listings; null fields do not filter.</summary>
/// <param name="CharacterOwnerUserId">Only transactions of characters owned by this user.</param>
public sealed record TransactionQuery(Guid CampaignId, Guid? CharacterId = null, Guid? CharacterOwnerUserId = null);

/// <summary>
/// A transaction with the names the API shows next to it. <see cref="ShopName"/> is null for party
/// stash operations, <see cref="CharacterName"/> for DM operations without a character and
/// <see cref="ActorDisplayName"/> for records without an actor.
/// </summary>
public sealed record TransactionView(Transaction Transaction, string? ShopName, string? CharacterName, string? ActorDisplayName);

public interface ITransactionRepository
{
    /// <summary>Read-only page of transactions matching the query, newest first.</summary>
    Task<(IReadOnlyList<TransactionView> Items, int Total)> ListViewsAsync(TransactionQuery query, int skip, int take, CancellationToken cancellationToken = default);

    /// <summary>One saved transaction with its names, or null.</summary>
    Task<TransactionView?> GetViewAsync(Guid id, CancellationToken cancellationToken = default);

    void Add(Transaction transaction);
}
