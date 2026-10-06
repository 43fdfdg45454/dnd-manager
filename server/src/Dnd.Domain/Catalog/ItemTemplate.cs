using Dnd.Domain.Common;
using Dnd.Domain.Items;

namespace Dnd.Domain.Catalog;

/// <summary>
/// Item definition. Catalog items (SRD and content packs) have <see cref="CampaignId"/> null and a
/// dataset <see cref="Index"/>; homebrew items belong to a campaign and have no index. The Guid id
/// stays stable across dataset re-imports so that inventories can keep referencing it.
/// </summary>
public sealed class ItemTemplate : EntityBase
{
    public const int NameMaxLength = 200;

    private ItemTemplate()
    {
    }

    /// <summary>Null for SRD items.</summary>
    public Guid? CampaignId { get; private set; }

    /// <summary>Dataset slug for catalog items (SRD and content packs); null for homebrew.</summary>
    public string? Index { get; private set; }

    /// <summary>"srd", "homebrew" or the id of the content pack (see <see cref="CatalogSources"/>).</summary>
    public string Source { get; private set; } = CatalogSources.Srd;

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

    /// <summary>Free-text effects of homebrew items; empty for SRD items.</summary>
    public IReadOnlyList<string> Effects { get; private set; } = [];

    /// <summary>Structured effects on the sheet while the item is active (see <see cref="ItemModifier"/>).</summary>
    public IReadOnlyList<ItemModifier> Modifiers { get; private set; } = [];

    /// <summary>True for catalog items (SRD and content packs), which no campaign can edit.</summary>
    public bool IsSrd => CampaignId is null;

    /// <summary>True when the item can be used inside the campaign: SRD items and the campaign's own homebrew.</summary>
    public bool IsVisibleIn(Guid campaignId) => CampaignId is null || CampaignId == campaignId;

    public static ItemTemplate CreateSrd(string index, ItemTemplateData data, DateTimeOffset now) =>
        CreateCatalog(CatalogSources.Srd, index, data, now);

    /// <summary>Creates a catalog item of the SRD or of a content pack (<paramref name="source"/> = pack id).</summary>
    public static ItemTemplate CreateCatalog(string source, string index, ItemTemplateData data, DateTimeOffset now)
    {
        var item = new ItemTemplate { Index = index, Source = source, CreatedAt = now };
        item.Apply(data);
        return item;
    }

    /// <summary>Creates a homebrew item that belongs to a campaign (it has no dataset index).</summary>
    public static ItemTemplate CreateHomebrew(Guid campaignId, ItemTemplateData data, DateTimeOffset now)
    {
        var item = new ItemTemplate { CampaignId = campaignId, Source = CatalogSources.Homebrew, CreatedAt = now };
        item.Apply(data);
        return item;
    }

    /// <summary>Replaces the rules data of a homebrew item.</summary>
    public void UpdateHomebrew(ItemTemplateData data)
    {
        if (IsSrd)
        {
            throw DomainException.RuleViolation("Los objetos del SRD no se pueden editar.");
        }

        Apply(data);
    }

    /// <summary>Snapshot of the rules data (e.g. to merge a partial edit).</summary>
    public ItemTemplateData ToData() => new()
    {
        Name = Name,
        Category = Category,
        Subcategory = Subcategory,
        Rarity = Rarity,
        RequiresAttunement = RequiresAttunement,
        CostCp = CostCp,
        WeightLb = WeightLb,
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
        Description = Description,
        Effects = Effects,
        Modifiers = Modifiers,
    };

    /// <summary>Replaces the rules data of a catalog item (SRD or content pack) with a newer version of its dataset.</summary>
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

        if (!Enum.IsDefined(data.Category) || (data.Rarity is { } rarity && !Enum.IsDefined(rarity)))
        {
            throw DomainException.RuleViolation("La categoría o la rareza del objeto no son válidas.");
        }

        var modifiers = ItemModifier.NormalizeAll(data.Modifiers) ?? [];

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
        Effects = data.Effects;
        Modifiers = modifiers;
    }
}
