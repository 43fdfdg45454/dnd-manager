namespace Dnd.Domain.Catalog;

/// <summary>Rules data of an item template, shared by the SRD import and (later) homebrew items.</summary>
public sealed record ItemTemplateData
{
    public required string Name { get; init; }

    public ItemCategory Category { get; init; }

    /// <summary>E.g. "Simple Melee", "Light Armor", "Potion".</summary>
    public string Subcategory { get; init; } = string.Empty;

    public ItemRarity? Rarity { get; init; }

    public bool RequiresAttunement { get; init; }

    /// <summary>Cost in copper pieces; null when unknown (magic items).</summary>
    public int? CostCp { get; init; }

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
}
