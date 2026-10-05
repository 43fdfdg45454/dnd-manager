using Dnd.Domain.Common;

namespace Dnd.Domain.Catalog;

/// <summary>
/// Item definition. SRD items have <see cref="CampaignId"/> null and a dataset <see cref="Index"/>;
/// homebrew items belong to a campaign and have no index. The Guid id stays stable across
/// dataset re-imports so that future inventories can reference it.
/// </summary>
public sealed class ItemTemplate : EntityBase
{
    public const int NameMaxLength = 200;

    private ItemTemplate()
    {
    }

    /// <summary>Null for SRD items.</summary>
    public Guid? CampaignId { get; private set; }

    /// <summary>Dataset slug for SRD items; null for homebrew.</summary>
    public string? Index { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public ItemCategory Category { get; private set; }

    public string Subcategory { get; private set; } = string.Empty;

    public ItemRarity? Rarity { get; private set; }

    public bool RequiresAttunement { get; private set; }

    public int? CostCp { get; private set; }

    public decimal? WeightLb { get; private set; }

    public string? DamageDice { get; private set; }

    public string? DamageType { get; private set; }

    public string? VersatileDice { get; private set; }

    public IReadOnlyList<string> Properties { get; private set; } = [];

    public int? RangeNormal { get; private set; }

    public int? RangeLong { get; private set; }

    public int? ArmorClassBase { get; private set; }

    public bool? AddDexModifier { get; private set; }

    public int? MaxDexBonus { get; private set; }

    public int? StrengthMinimum { get; private set; }

    public bool StealthDisadvantage { get; private set; }

    public IReadOnlyList<string> Description { get; private set; } = [];

    public bool IsSrd => CampaignId is null;

    public static ItemTemplate CreateSrd(string index, ItemTemplateData data, DateTimeOffset now)
    {
        var item = new ItemTemplate { Index = index, CreatedAt = now };
        item.Apply(data);
        return item;
    }

    /// <summary>Replaces the rules data of an SRD item with a newer version of the dataset.</summary>
    public void UpdateSrd(ItemTemplateData data)
    {
        if (!IsSrd)
        {
            throw DomainException.RuleViolation("Solo se pueden actualizar así los objetos del SRD.");
        }

        Apply(data);
    }

    private void Apply(ItemTemplateData data)
    {
        var name = data.Name.Trim();
        if (name.Length is 0 or > NameMaxLength)
        {
            throw DomainException.RuleViolation($"El nombre debe tener entre 1 y {NameMaxLength} caracteres.");
        }

        Name = name;
        Category = data.Category;
        Subcategory = data.Subcategory;
        Rarity = data.Rarity;
        RequiresAttunement = data.RequiresAttunement;
        CostCp = data.CostCp;
        WeightLb = data.WeightLb;
        DamageDice = data.DamageDice;
        DamageType = data.DamageType;
        VersatileDice = data.VersatileDice;
        Properties = data.Properties;
        RangeNormal = data.RangeNormal;
        RangeLong = data.RangeLong;
        ArmorClassBase = data.ArmorClassBase;
        AddDexModifier = data.AddDexModifier;
        MaxDexBonus = data.MaxDexBonus;
        StrengthMinimum = data.StrengthMinimum;
        StealthDisadvantage = data.StealthDisadvantage;
        Description = data.Description;
    }
}
