using OpenTrpg.Core.Domain.Common;

namespace OpenTrpg.Core.Domain.Items;

/// <summary>
/// A campaign shop with its stock (<see cref="ShopItem"/>). Players only see and trade with open
/// shops; DMs manage them. Purchases and sales are priced here and applied to the character by the
/// caller in the same unit of work.
/// </summary>
public sealed class Shop : EntityBase
{
    public const int NameMaxLength = 100;
    public const int DescriptionMaxLength = 5000;
    public const int DefaultBuybackPercent = 50;

    private readonly List<ShopItem> _items = [];

    private Shop()
    {
    }

    public Guid CampaignId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public bool IsOpen { get; private set; }

    /// <summary>Percentage of the reference price paid to characters who sell an item (0-100).</summary>
    public int BuybackPercent { get; private set; } = DefaultBuybackPercent;

    public DateTimeOffset UpdatedAt { get; private set; }

    public IReadOnlyCollection<ShopItem> Items => _items;

    /// <summary>Creates a closed shop: the DM stocks it and opens it when ready.</summary>
    public static Shop Create(Guid campaignId, string name, string? description, int? buybackPercent, DateTimeOffset now)
    {
        var shop = new Shop
        {
            CampaignId = campaignId,
            Name = NormalizeName(name),
            Description = NormalizeDescription(description),
            BuybackPercent = ValidateBuyback(buybackPercent ?? DefaultBuybackPercent),
            CreatedAt = now,
            UpdatedAt = now,
        };
        return shop;
    }

    /// <summary>Null arguments are left unchanged; an empty description clears it.</summary>
    public void Update(string? name, string? description, bool? isOpen, int? buybackPercent, DateTimeOffset now)
    {
        var newName = name is null ? Name : NormalizeName(name);
        var newDescription = description is null ? Description : NormalizeDescription(description);
        var newBuyback = buybackPercent is null ? BuybackPercent : ValidateBuyback(buybackPercent.Value);

        Name = newName;
        Description = newDescription;
        IsOpen = isOpen ?? IsOpen;
        BuybackPercent = newBuyback;
        UpdatedAt = now;
    }

    /// <summary>Trading needs an open shop (409 otherwise).</summary>
    public void EnsureOpen()
    {
        if (!IsOpen)
        {
            throw DomainException.Conflict("La tienda está cerrada.");
        }
    }

    public ShopItem FindItem(Guid shopItemId) =>
        _items.FirstOrDefault(i => i.Id == shopItemId) ?? throw DomainException.NotFound("El objeto no está en la tienda.");

    /// <summary><paramref name="overrides"/> must be an instance owned by nobody else.</summary>
    public ShopItem AddItem(Guid? templateId, ItemOverrides overrides, int priceCp, int? stock, DateTimeOffset now)
    {
        var normalized = overrides.Normalize();
        if (templateId is null && normalized.Name is null)
        {
            throw DomainException.RuleViolation("Un objeto sin plantilla necesita un nombre.");
        }

        var sortOrder = _items.Count == 0 ? 0 : _items.Max(i => i.SortOrder) + 1;
        var item = ShopItem.Create(Id, templateId, normalized, priceCp, stock, sortOrder);
        _items.Add(item);
        UpdatedAt = now;
        return item;
    }

    public void RemoveItem(Guid shopItemId, DateTimeOffset now)
    {
        _items.Remove(FindItem(shopItemId));
        UpdatedAt = now;
    }

    /// <summary>
    /// Takes <paramref name="quantity"/> units of a shop item for a purchase (checks the shop is open
    /// and there is stock) and returns the total price in copper pieces.
    /// </summary>
    public long SellToCharacter(Guid shopItemId, int quantity, DateTimeOffset now)
    {
        EnsureOpen();
        var item = FindItem(shopItemId);
        item.TakeStock(quantity);
        UpdatedAt = now;
        return (long)item.Price * quantity;
    }

    /// <summary>
    /// The shop item a character's item matches when it is sold back: same template, preferring an
    /// entry without overrides. Items without template match nothing.
    /// </summary>
    public ShopItem? FindMatching(Guid? templateId) => templateId is null
        ? null
        : _items.Where(i => i.TemplateId == templateId).OrderBy(i => i.Overrides.IsEmpty ? 0 : 1).ThenBy(i => i.SortOrder).FirstOrDefault();

    /// <summary>
    /// Buys units from a character: pays <see cref="BuybackPercent"/> of <paramref name="referencePriceCp"/>
    /// per unit (rounded down) and returns the units to the stock of the matching shop item when it
    /// tracks stock. Returns the total paid in copper pieces.
    /// </summary>
    public long BuyFromCharacter(ShopItem? matching, int referencePriceCp, int quantity, DateTimeOffset now)
    {
        EnsureOpen();
        if (referencePriceCp < 0 || quantity < 1)
        {
            throw DomainException.RuleViolation("El precio o la cantidad no son válidos.");
        }

        matching?.ReturnStock(quantity);
        UpdatedAt = now;
        return (long)referencePriceCp * quantity * BuybackPercent / 100;
    }

    private static string NormalizeName(string name)
    {
        var trimmed = (name ?? string.Empty).Trim();
        if (trimmed.Length is 0 or > NameMaxLength)
        {
            throw DomainException.RuleViolation($"El nombre de la tienda debe tener entre 1 y {NameMaxLength} caracteres.");
        }

        return trimmed;
    }

    private static string? NormalizeDescription(string? description)
    {
        var trimmed = description?.Trim();
        if (trimmed is { Length: > DescriptionMaxLength })
        {
            throw DomainException.RuleViolation($"La descripción no puede superar los {DescriptionMaxLength} caracteres.");
        }

        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    private static int ValidateBuyback(int percent) => percent is >= 0 and <= 100
        ? percent
        : throw DomainException.RuleViolation("El porcentaje de recompra debe estar entre 0 y 100.");
}
