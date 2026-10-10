using OpenTrpg.Core.Domain.Common;

namespace OpenTrpg.Core.Domain.Items;

/// <summary>
/// An item offered by a <see cref="Shop"/>: template and/or overrides, unit price and stock (null =
/// unlimited). <see cref="Version"/> is bumped on every change and used as an optimistic concurrency
/// token, so two concurrent purchases of the last unit cannot both succeed.
/// </summary>
public sealed class ShopItem
{
    private ShopItem()
    {
    }

    public Guid Id { get; init; } = Guid.NewGuid();

    public Guid ShopId { get; private set; }

    public Guid? TemplateId { get; private set; }

    public ItemOverrides Overrides { get; private set; } = ItemOverrides.None();

    /// <summary>Unit price in copper pieces.</summary>
    public int Price { get; private set; }

    /// <summary>Units left; null = unlimited.</summary>
    public int? Stock { get; private set; }

    public int SortOrder { get; private set; }

    /// <summary>Incremented on every change; optimistic concurrency token.</summary>
    public int Version { get; private set; }

    internal static ShopItem Create(Guid shopId, Guid? templateId, ItemOverrides overrides, int priceCp, int? stock, int sortOrder)
    {
        ValidatePrice(priceCp);
        ValidateStock(stock);
        return new ShopItem
        {
            ShopId = shopId,
            TemplateId = templateId,
            Overrides = overrides,
            Price = priceCp,
            Stock = stock,
            SortOrder = sortOrder,
        };
    }

    /// <summary>Null arguments are left unchanged. <paramref name="setStock"/> applies <paramref name="stock"/> (null = unlimited).</summary>
    public void Update(int? priceCp, bool setStock, int? stock, ItemOverrides? overrides, int? sortOrder)
    {
        if (priceCp is { } price)
        {
            ValidatePrice(price);
        }

        if (setStock)
        {
            ValidateStock(stock);
        }

        var normalized = overrides?.Normalize();
        if (normalized is not null && TemplateId is null && normalized.Name is null)
        {
            throw DomainException.RuleViolation("Un objeto sin plantilla necesita un nombre.");
        }

        Price = priceCp ?? Price;
        if (setStock)
        {
            Stock = stock;
        }

        Overrides = normalized ?? Overrides;
        SortOrder = sortOrder ?? SortOrder;
        Version++;
    }

    internal void TakeStock(int quantity)
    {
        if (quantity is < 1 or > ItemLimits.MaxQuantity)
        {
            throw DomainException.RuleViolation($"La cantidad debe estar entre 1 y {ItemLimits.MaxQuantity}.");
        }

        if (Stock is { } stock)
        {
            if (stock < quantity)
            {
                throw DomainException.RuleViolation(stock == 0 ? "No queda stock de este objeto." : $"Solo quedan {stock} unidades de este objeto.");
            }

            Stock = stock - quantity;
            Version++;
        }
    }

    internal void ReturnStock(int quantity)
    {
        if (Stock is { } stock)
        {
            Stock = (int)Math.Min((long)stock + quantity, ItemLimits.MaxStock);
            Version++;
        }
    }

    private static void ValidatePrice(int priceCp)
    {
        if (priceCp is < 0 or > ItemLimits.MaxCostCp)
        {
            throw DomainException.RuleViolation($"El precio debe estar entre 0 y {ItemLimits.MaxCostCp} pc.");
        }
    }

    private static void ValidateStock(int? stock)
    {
        if (stock is < 0 or > ItemLimits.MaxStock)
        {
            throw DomainException.RuleViolation($"El stock debe estar entre 0 y {ItemLimits.MaxStock}.");
        }
    }
}
