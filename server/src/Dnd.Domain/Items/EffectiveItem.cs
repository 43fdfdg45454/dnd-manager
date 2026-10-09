using Dnd.Domain.Catalog;

namespace Dnd.Domain.Items;

/// <summary>
/// The item as it is used in play: template data with the overrides applied on top (pure).
/// </summary>
public sealed record EffectiveItem
{
    /// <summary>Name shown for an item without template whose overrides lack a name (should not happen: it is validated).</summary>
    public const string UnnamedItem = "Objeto sin nombre";

    private static readonly string[] ConsumableSubcategories = ["Potion", "Scroll", "Ammunition"];

    public required string Name { get; init; }

    public ItemCategory Category { get; init; }

    /// <summary>Template subcategory ("Martial Melee", "Potion"); null without template or when empty.</summary>
    public string? Subcategory { get; init; }

    public ItemRarity? Rarity { get; init; }

    public bool RequiresAttunement { get; init; }

    public decimal? WeightLb { get; init; }

    public string? DamageDice { get; init; }

    public string? DamageType { get; init; }

    public string? VersatileDice { get; init; }

    public IReadOnlyList<string> Properties { get; init; } = [];

    public int? RangeNormal { get; init; }

    public int? RangeLong { get; init; }

    /// <summary>Base armor class of armor (for shields, the shield bonus); null when the item gives none.</summary>
    public int? ArmorClassBase { get; init; }

    public bool AddDexModifier { get; init; }

    public int? MaxDexBonus { get; init; }

    public int? StrengthMinimum { get; init; }

    public bool StealthDisadvantage { get; init; }

    /// <summary>Sum of the <see cref="ItemModifierKind.AttackBonus"/> modifiers (kept for API compatibility).</summary>
    public int AttackBonus => SumOf(ItemModifierKind.AttackBonus);

    /// <summary>Sum of the <see cref="ItemModifierKind.DamageBonus"/> modifiers (kept for API compatibility).</summary>
    public int DamageBonus => SumOf(ItemModifierKind.DamageBonus);

    public IReadOnlyList<string> Effects { get; init; } = [];

    /// <summary>
    /// Structured modifiers: the overrides' list, or the template's when the overrides do not define one,
    /// followed by the legacy <see cref="ItemOverrides.AttackBonus"/>/<see cref="ItemOverrides.DamageBonus"/>
    /// converted to modifiers. They apply only while the item is active (<see cref="IsActiveFor(CharacterItem)"/>).
    /// </summary>
    public IReadOnlyList<ItemModifier> Modifiers { get; init; } = [];

    public IReadOnlyList<string> Description { get; init; } = [];

    /// <summary>No template, or the template with at least one override.</summary>
    public bool IsCustom { get; init; }

    /// <summary>Consumables: the Consumable category, plus potions, scrolls and ammunition of any category.</summary>
    public bool IsConsumable => Category == ItemCategory.Consumable
        || (Subcategory is { } sub && ConsumableSubcategories.Contains(sub, StringComparer.OrdinalIgnoreCase));

    /// <summary>
    /// Hint for stacking equal items in one inventory entry: consumables, ammunition and adventuring
    /// gear (the caller also requires the entry to have no charges).
    /// </summary>
    public bool IsStackable => IsConsumable || Category == ItemCategory.AdventuringGear;

    /// <summary>
    /// Weapons, armor, shields and magic items (rings, cloaks and gauntlets are worn) can be equipped, and so
    /// can any other non-consumable item that does something when worn: structured modifiers, free-text
    /// effects, an armor class, a damage die or attunement (custom items without an equipment category).
    /// </summary>
    public bool IsEquippable =>
        Category is ItemCategory.Weapon or ItemCategory.Armor or ItemCategory.Shield or ItemCategory.MagicItem
        || (!IsConsumable
            && (Modifiers.Count > 0 || Effects.Count > 0 || ArmorClassBase is not null || DamageDice is not null || RequiresAttunement));

    /// <summary>Whether the modifiers of the item apply to the character that has <paramref name="item"/>: equipped and, when required, attuned.</summary>
    public bool IsActiveFor(CharacterItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return IsActive(item.Equipped, item.Attuned);
    }

    /// <summary>Whether the modifiers apply for an entry in that state: equipped and, when the item requires attunement, attuned.</summary>
    public bool IsActive(bool equipped, bool attuned) => equipped && (!RequiresAttunement || attuned);

    /// <summary>Resolves template ∪ overrides. Overrides replace only the fields they define.</summary>
    public static EffectiveItem Resolve(ItemTemplate? template, ItemOverrides overrides)
    {
        ArgumentNullException.ThrowIfNull(overrides);

        return new EffectiveItem
        {
            Name = overrides.Name ?? template?.Name ?? UnnamedItem,
            Category = overrides.Category ?? template?.Category ?? ItemCategory.Other,
            Subcategory = string.IsNullOrEmpty(template?.Subcategory) ? null : template.Subcategory,
            Rarity = overrides.Rarity ?? template?.Rarity,
            RequiresAttunement = overrides.RequiresAttunement ?? template?.RequiresAttunement ?? false,
            WeightLb = overrides.WeightLb ?? template?.WeightLb,
            DamageDice = overrides.DamageDice ?? template?.DamageDice,
            DamageType = overrides.DamageType ?? template?.DamageType,
            VersatileDice = overrides.VersatileDice ?? template?.VersatileDice,
            Properties = overrides.Properties ?? template?.Properties ?? [],
            RangeNormal = overrides.RangeNormal ?? template?.RangeNormal,
            RangeLong = overrides.RangeLong ?? template?.RangeLong,
            ArmorClassBase = overrides.ArmorClassBase ?? template?.ArmorClassBase,
            AddDexModifier = overrides.AddDexModifier ?? template?.AddDexModifier ?? false,
            MaxDexBonus = overrides.MaxDexBonus ?? template?.MaxDexBonus,
            StrengthMinimum = overrides.StrengthMinimum ?? template?.StrengthMinimum,
            StealthDisadvantage = overrides.StealthDisadvantage ?? template?.StealthDisadvantage ?? false,
            Modifiers = ResolveModifiers(template, overrides),
            Effects = overrides.Effects ?? template?.Effects ?? [],
            Description = overrides.Description ?? template?.Description ?? [],
            IsCustom = template is null || !overrides.IsEmpty,
        };
    }

    private static IReadOnlyList<ItemModifier> ResolveModifiers(ItemTemplate? template, ItemOverrides overrides)
    {
        var modifiers = overrides.Modifiers ?? template?.Modifiers ?? [];
        if (overrides.AttackBonus is null && overrides.DamageBonus is null)
        {
            return modifiers;
        }

        var result = modifiers.ToList();
        if (overrides.AttackBonus is { } attack)
        {
            result.Add(new ItemModifier(ItemModifierKind.AttackBonus, null, attack));
        }

        if (overrides.DamageBonus is { } damage)
        {
            result.Add(new ItemModifier(ItemModifierKind.DamageBonus, null, damage));
        }

        return result;
    }

    private int SumOf(ItemModifierKind kind) => Modifiers.Where(m => m.Kind == kind).Sum(m => m.Value);
}
