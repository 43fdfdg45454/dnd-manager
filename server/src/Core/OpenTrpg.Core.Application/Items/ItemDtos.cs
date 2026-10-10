using System.Text.Json.Serialization;
using OpenTrpg.Core.Application.Abstractions.Persistence;
using OpenTrpg.Core.Application.Common;
using OpenTrpg.Core.Domain.Catalog;
using OpenTrpg.Core.Domain.Items;

namespace OpenTrpg.Core.Application.Items;

/// <summary>
/// A structured item modifier (<see cref="ItemModifier"/>). <see cref="Kind"/> is an
/// <see cref="ItemModifierKind"/> name; <see cref="Target"/> an ability or skill index, or null.
/// </summary>
public sealed record ItemModifierDto(string Kind, string? Target, int Value)
{
    public static ItemModifierDto From(ItemModifier m) => new(m.Kind.ToString(), m.Target, m.Value);

    public static IReadOnlyList<ItemModifierDto> FromAll(IEnumerable<ItemModifier> modifiers) => modifiers.Select(From).ToList();

    /// <summary>Maps to the domain (not normalized). Call only on modifiers accepted by <see cref="ItemModifierDtoValidator"/>.</summary>
    public ItemModifier ToDomain() => new(EnumNames.Parse<ItemModifierKind>(Kind), Target, Value);
}

/// <summary>
/// Overridden fields of an item, used both in requests and responses. Only the defined fields are
/// written to JSON. <see cref="Category"/> and <see cref="Rarity"/> are enum names
/// (<see cref="ItemCategory"/>, <see cref="ItemRarity"/>). An empty list or blank text counts as not overridden,
/// except for <see cref="Modifiers"/>: absent or null keeps the template's, an empty list removes them.
/// </summary>
public sealed record ItemOverridesDto
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Name { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? Description { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Category { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? DamageDice { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? DamageType { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? VersatileDice { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? Properties { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? RangeNormal { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? RangeLong { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? ArmorClassBase { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? AddDexModifier { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? MaxDexBonus { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? StrengthMinimum { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? StealthDisadvantage { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public decimal? WeightLb { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Rarity { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? RequiresAttunement { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? AttackBonus { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? DamageBonus { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? Effects { get; init; }

    /// <summary>Null (or absent) keeps the template's modifiers; an empty list removes them all.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<ItemModifierDto>? Modifiers { get; init; }

    public static ItemOverridesDto From(ItemOverrides o) => new()
    {
        Name = o.Name,
        Description = o.Description,
        Category = o.Category?.ToString(),
        DamageDice = o.DamageDice,
        DamageType = o.DamageType,
        VersatileDice = o.VersatileDice,
        Properties = o.Properties,
        RangeNormal = o.RangeNormal,
        RangeLong = o.RangeLong,
        ArmorClassBase = o.ArmorClassBase,
        AddDexModifier = o.AddDexModifier,
        MaxDexBonus = o.MaxDexBonus,
        StrengthMinimum = o.StrengthMinimum,
        StealthDisadvantage = o.StealthDisadvantage,
        WeightLb = o.WeightLb,
        Rarity = o.Rarity?.ToString(),
        RequiresAttunement = o.RequiresAttunement,
        AttackBonus = o.AttackBonus,
        DamageBonus = o.DamageBonus,
        Effects = o.Effects,
        Modifiers = o.Modifiers is null ? null : ItemModifierDto.FromAll(o.Modifiers),
    };

    /// <summary>Maps to a new domain instance. Call only on overrides accepted by <see cref="ItemOverridesDtoValidator"/>.</summary>
    public ItemOverrides ToDomain() => new()
    {
        Name = Name,
        Description = Description,
        Category = Category is null ? null : EnumNames.Parse<ItemCategory>(Category),
        DamageDice = DamageDice,
        DamageType = DamageType,
        VersatileDice = VersatileDice,
        Properties = Properties,
        RangeNormal = RangeNormal,
        RangeLong = RangeLong,
        ArmorClassBase = ArmorClassBase,
        AddDexModifier = AddDexModifier,
        MaxDexBonus = MaxDexBonus,
        StrengthMinimum = StrengthMinimum,
        StealthDisadvantage = StealthDisadvantage,
        WeightLb = WeightLb,
        Rarity = Rarity is null ? null : EnumNames.Parse<ItemRarity>(Rarity),
        RequiresAttunement = RequiresAttunement,
        AttackBonus = AttackBonus,
        DamageBonus = DamageBonus,
        Effects = Effects,
        Modifiers = Modifiers?.Select(m => m.ToDomain()).ToArray(),
    };
}

public sealed record DamageDto(string Dice, string? Type, string? Versatile);

public sealed record RangeDto(int Normal, int? Long);

public sealed record ArmorDto(int Base, bool AddDex, int? MaxDex, int? StrengthMinimum, bool StealthDisadvantage);

/// <summary>
/// Template ∪ overrides, as used in play. <see cref="AttackBonus"/>/<see cref="DamageBonus"/> are the sums
/// of the corresponding <see cref="Modifiers"/> (kept for compatibility).
/// </summary>
public sealed record EffectiveItemDto(
    string Name,
    string Category,
    string? Subcategory,
    string? Rarity,
    bool RequiresAttunement,
    decimal? WeightLb,
    DamageDto? Damage,
    IReadOnlyList<string> Properties,
    RangeDto? Range,
    ArmorDto? Armor,
    int AttackBonus,
    int DamageBonus,
    IReadOnlyList<string> Effects,
    IReadOnlyList<string> Description,
    IReadOnlyList<ItemModifierDto> Modifiers)
{
    public static EffectiveItemDto From(EffectiveItem e) => new(
        e.Name,
        e.Category.ToString(),
        e.Subcategory,
        e.Rarity?.ToString(),
        e.RequiresAttunement,
        e.WeightLb,
        e.DamageDice is { } dice ? new DamageDto(dice, e.DamageType, e.VersatileDice) : null,
        e.Properties,
        e.RangeNormal is { } normal ? new RangeDto(normal, e.RangeLong) : null,
        e.ArmorClassBase is { } armorBase ? new ArmorDto(armorBase, e.AddDexModifier, e.MaxDexBonus, e.StrengthMinimum, e.StealthDisadvantage) : null,
        e.AttackBonus,
        e.DamageBonus,
        e.Effects,
        e.Description,
        ItemModifierDto.FromAll(e.Modifiers));
}

/// <param name="TemplateName">Name of the template (null for items made by hand).</param>
/// <param name="Overrides">Only the overridden fields.</param>
public sealed record CharacterItemDto(
    Guid Id,
    Guid? TemplateId,
    string? TemplateName,
    int Quantity,
    bool Equipped,
    bool Attuned,
    int? Charges,
    int? ChargesMax,
    string? Notes,
    int SortOrder,
    ItemOverridesDto Overrides,
    EffectiveItemDto Effective,
    bool IsCustom)
{
    public static CharacterItemDto From(CharacterItem item, ItemTemplate? template)
    {
        var effective = EffectiveItem.Resolve(template, item.Overrides);
        return new CharacterItemDto(
            item.Id,
            item.TemplateId,
            template?.Name,
            item.Quantity,
            item.Equipped,
            item.Attuned,
            item.Charges,
            item.ChargesMax,
            item.Notes,
            item.SortOrder,
            ItemOverridesDto.From(item.Overrides),
            EffectiveItemDto.From(effective),
            effective.IsCustom);
    }
}

/// <param name="CarryCapacityLb">Strength score × 15 (informative).</param>
public sealed record InventoryDto(
    IReadOnlyList<CharacterItemDto> Items,
    int CopperPieces,
    decimal TotalWeightLb,
    int CarryCapacityLb,
    int AttunedCount);

/// <param name="Stock">Units left; null = unlimited.</param>
public sealed record ShopItemDto(
    Guid Id,
    Guid? TemplateId,
    int PriceCp,
    int? Stock,
    int SortOrder,
    ItemOverridesDto Overrides,
    EffectiveItemDto Effective)
{
    public static ShopItemDto From(ShopItem item, ItemTemplate? template) => new(
        item.Id,
        item.TemplateId,
        item.Price,
        item.Stock,
        item.SortOrder,
        ItemOverridesDto.From(item.Overrides),
        EffectiveItemDto.From(EffectiveItem.Resolve(template, item.Overrides)));
}

public sealed record ShopDto(
    Guid Id,
    Guid CampaignId,
    string Name,
    string? Description,
    bool IsOpen,
    int BuybackPercent,
    IReadOnlyList<ShopItemDto> Items);

public sealed record ShopSummaryDto(Guid Id, Guid CampaignId, string Name, string? Description, bool IsOpen, int BuybackPercent, int ItemCount);

/// <param name="ShopId">Null for party stash operations.</param>
/// <param name="CharacterId">Null for DM operations on the party stash without a character.</param>
/// <param name="ActorDisplayName">Who performed the operation (null for old records).</param>
/// <param name="Type">
/// "Purchase", "Sale", "StashAdd", "StashRemove", "StashTake", "StashReturn", "StashGoldAdd" or "StashGoldSplit".
/// </param>
public sealed record TransactionDto(
    Guid Id,
    Guid? ShopId,
    string? ShopName,
    Guid? CharacterId,
    string? CharacterName,
    Guid? ActorUserId,
    string? ActorDisplayName,
    string Type,
    string ItemName,
    int Quantity,
    int TotalCp,
    DateTimeOffset At)
{
    public static TransactionDto From(TransactionView view)
    {
        var t = view.Transaction;
        return new TransactionDto(
            t.Id, t.ShopId, view.ShopName, t.CharacterId, view.CharacterName, t.ActorUserId, view.ActorDisplayName,
            t.Type.ToString(), t.ItemName, t.Quantity, t.Total, t.At);
    }
}

/// <summary>Result of a purchase or sale: the character's inventory afterwards and the recorded transaction.</summary>
public sealed record TradeResultDto(InventoryDto Inventory, TransactionDto Transaction);
