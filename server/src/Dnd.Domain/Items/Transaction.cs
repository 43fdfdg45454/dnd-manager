using Dnd.Domain.Common;

namespace Dnd.Domain.Items;

public enum TransactionType
{
    /// <summary>A character bought from a shop.</summary>
    Purchase,

    /// <summary>A character sold to a shop.</summary>
    Sale,
}

/// <summary>Record of a purchase or sale between a character and a shop (immutable).</summary>
public sealed class Transaction : EntityBase
{
    private Transaction()
    {
    }

    public Guid CampaignId { get; private set; }

    public Guid ShopId { get; private set; }

    public Guid CharacterId { get; private set; }

    public TransactionType Type { get; private set; }

    /// <summary>Effective name of the item at the time of the operation.</summary>
    public string ItemName { get; private set; } = string.Empty;

    public int Quantity { get; private set; }

    /// <summary>Money that changed hands, in copper pieces (always ≥ 0).</summary>
    public int TotalCp { get; private set; }

    public DateTimeOffset At { get; private set; }

    public static Transaction Record(Guid campaignId, Guid shopId, Guid characterId, TransactionType type, string itemName, int quantity, long totalCp, DateTimeOffset now)
    {
        if (quantity < 1 || totalCp is < 0 or > int.MaxValue)
        {
            throw DomainException.RuleViolation("La cantidad o el importe de la operación no son válidos.");
        }

        return new Transaction
        {
            CampaignId = campaignId,
            ShopId = shopId,
            CharacterId = characterId,
            Type = type,
            ItemName = itemName.Length > ItemLimits.NameMaxLength ? itemName[..ItemLimits.NameMaxLength] : itemName,
            Quantity = quantity,
            TotalCp = (int)totalCp,
            At = now,
            CreatedAt = now,
        };
    }
}
