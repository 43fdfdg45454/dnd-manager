using OpenTrpg.Core.Domain.Items;

namespace OpenTrpg.Core.Domain.Catalog;

/// <summary>Rules data of an item template, shared by the SRD import and campaign homebrew items.</summary>
public sealed record ItemTemplateData
{
    public required string Name { get; init; }

    public ItemCategory Category { get; init; }

    /// <summary>E.g. "Simple Melee", "Light Armor", "Potion".</summary>
    public string Subcategory { get; init; } = string.Empty;

    public ItemRarity? Rarity { get; init; }

    public bool RequiresAttunement { get; init; }

    /// <summary>Cost in copper pieces; null when unknown (magic items).</summary>
    public int? Cost { get; init; }

    public decimal? WeightLb { get; init; }

    public string? DamageDice { get; init; }

    public string? DamageType { get; init; }

    /// <summary>Two-handed damage of versatile weapons.</summary>
    public string? VersatileDice { get; init; }

    /// <summary>Weapon property indexes, e.g. "finesse", "versatile".</summary>
    public IReadOnlyList<string> Properties { get; init; } = [];

    public int? RangeNormal { get; init; }

    public int? RangeLong { get; init; }

    public int? ArmorClassBase { get; init; }

    public bool? AddDexModifier { get; init; }

    public int? MaxDexBonus { get; init; }

    public int? StrengthMinimum { get; init; }

    public bool StealthDisadvantage { get; init; }

    public IReadOnlyList<string> Description { get; init; } = [];

    /// <summary>Free-text effects ("+1 to attack and damage rolls", "Light 20 ft"); empty for SRD items.</summary>
    public IReadOnlyList<string> Effects { get; init; } = [];

    /// <summary>Structured effects on the sheet while the item is active (see <see cref="ItemModifier"/>).</summary>
    public IReadOnlyList<ItemModifier> Modifiers { get; init; } = [];

    /// <summary>
    /// Extra rules data owned by the game system as a JSON object (e.g. D&amp;D 5e firearms: reload and misfire), or
    /// null. The core stores it as it is.
    /// </summary>
    public string? SystemDataJson { get; init; }
}
