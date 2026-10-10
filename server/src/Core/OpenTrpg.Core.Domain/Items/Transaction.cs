using OpenTrpg.Core.Domain.Common;

namespace OpenTrpg.Core.Domain.Items;

public enum TransactionType
{
    /// <summary>A character bought from a shop.</summary>
    Purchase,

    /// <summary>A character sold to a shop.</summary>
    Sale,

    /// <summary>A DM added loot to the party stash.</summary>
    StashAdd,

    /// <summary>A DM removed loot from the party stash.</summary>
    StashRemove,

    /// <summary>A character took an item from the party stash.</summary>
    StashTake,

    /// <summary>A character gave an item back to the party stash.</summary>
    StashReturn,

    /// <summary>A DM added gold to the party stash, or withdrew it (see <see cref="Transaction.ItemName"/>).</summary>
    StashGoldAdd,

    /// <summary>A character received its share of the party stash gold.</summary>
    StashGoldSplit,
}

/// <summary>
/// Record of an operation that moved items or money (immutable): purchases and sales between a
/// character and a shop, and every movement of the party stash. The transaction log of the campaign
/// is its single ledger.
/// </summary>
public sealed class Transaction : EntityBase
{
    private Transaction()
    {
    }

    public Guid CampaignId { get; private set; }

    /// <summary>Shop of a purchase or sale; null for party stash operations.</summary>
    public Guid? ShopId { get; private set; }

    /// <summary>Character involved; null for operations of the DM on the party stash alone.</summary>
    public Guid? CharacterId { get; private set; }

    /// <summary>User who performed the operation (null for records older than this field).</summary>
    public Guid? ActorUserId { get; private set; }

    public TransactionType Type { get; private set; }

    /// <summary>Effective name of the item at the time of the operation (or a description of the money moved).</summary>
    public string ItemName { get; private set; } = string.Empty;

    public int Quantity { get; private set; }

    /// <summary>Money that changed hands, in copper pieces (always ≥ 0).</summary>
    public int Total { get; private set; }

    public DateTimeOffset At { get; private set; }

    public static Transaction Record(
        Guid campaignId,
        Guid? shopId,
        Guid? characterId,
        Guid? actorUserId,
        TransactionType type,
        string itemName,
        int quantity,
        long totalCp,
        DateTimeOffset now)
    {
        if (quantity < 1 || totalCp is < 0 or > int.MaxValue)
        {
            throw DomainException.RuleViolation("La cantidad o el importe de la operación no son válidos.");
        }

        if (!Enum.IsDefined(type))
        {
            throw DomainException.RuleViolation("El tipo de operación no es válido.");
        }

        return new Transaction
        {
            CampaignId = campaignId,
            ShopId = shopId,
            CharacterId = characterId,
            ActorUserId = actorUserId,
            Type = type,
            ItemName = itemName.Length > ItemLimits.NameMaxLength ? itemName[..ItemLimits.NameMaxLength] : itemName,
            Quantity = quantity,
            Total = (int)totalCp,
            At = now,
            CreatedAt = now,
        };
    }
}
