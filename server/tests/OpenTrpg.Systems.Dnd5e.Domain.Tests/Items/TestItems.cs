using OpenTrpg.Core.Domain.Catalog;
using OpenTrpg.Core.Domain.Items;
using static OpenTrpg.Systems.Dnd5e.Domain.Tests.Characters.TestCatalog;
using OpenTrpg.Systems.Dnd5e.Domain.Catalog;

namespace OpenTrpg.Systems.Dnd5e.Domain.Tests.Items;

/// <summary>SRD-like item templates for the inventory and shop tests.</summary>
internal static class TestItems
{
    public static readonly ItemTemplate ChainMail = ItemTemplate.CreateCatalog(Dnd5eCatalogSources.Srd, "chain-mail", new ItemTemplateData
    {
        Name = "Chain Mail",
        Category = ItemCategory.Armor,
        Subcategory = "Heavy Armor",
        CostCp = 7500,
        WeightLb = 55,
        ArmorClassBase = 16,
        AddDexModifier = false,
        StrengthMinimum = 13,
        StealthDisadvantage = true,
    }, Now);

    public static readonly ItemTemplate Leather = ItemTemplate.CreateCatalog(Dnd5eCatalogSources.Srd, "leather-armor", new ItemTemplateData
    {
        Name = "Leather Armor",
        Category = ItemCategory.Armor,
        Subcategory = "Light Armor",
        CostCp = 1000,
        WeightLb = 10,
        ArmorClassBase = 11,
        AddDexModifier = true,
    }, Now);

    public static readonly ItemTemplate Shield = ItemTemplate.CreateCatalog(Dnd5eCatalogSources.Srd, "shield", new ItemTemplateData
    {
        Name = "Shield",
        Category = ItemCategory.Shield,
        Subcategory = "Shield",
        CostCp = 1000,
        WeightLb = 6,
        ArmorClassBase = 2,
    }, Now);

    public static readonly ItemTemplate Longsword = ItemTemplate.CreateCatalog(Dnd5eCatalogSources.Srd, "longsword", new ItemTemplateData
    {
        Name = "Longsword",
        Category = ItemCategory.Weapon,
        Subcategory = "Martial Melee",
        CostCp = 1500,
        WeightLb = 3,
        DamageDice = "1d8",
        DamageType = "Slashing",
        VersatileDice = "1d10",
        Properties = ["versatile"],
        Description = ["A sword."],
    }, Now);

    public static readonly ItemTemplate Arrow = ItemTemplate.CreateCatalog(Dnd5eCatalogSources.Srd, "arrow", new ItemTemplateData
    {
        Name = "Arrow",
        Category = ItemCategory.AdventuringGear,
        Subcategory = "Ammunition",
        CostCp = 5,
        WeightLb = 0.05m,
    }, Now);

    public static readonly ItemTemplate Rope = ItemTemplate.CreateCatalog(Dnd5eCatalogSources.Srd, "rope", new ItemTemplateData
    {
        Name = "Rope",
        Category = ItemCategory.AdventuringGear,
        Subcategory = "Standard Gear",
        CostCp = 100,
        WeightLb = 10,
    }, Now);

    public static readonly ItemTemplate Healing = ItemTemplate.CreateCatalog(Dnd5eCatalogSources.Srd, "potion-of-healing", new ItemTemplateData
    {
        Name = "Potion of Healing",
        Category = ItemCategory.MagicItem,
        Subcategory = "Potion",
        Rarity = ItemRarity.Common,
    }, Now);

    public static ItemTemplate Attuned(string name) => ItemTemplate.CreateCatalog(Dnd5eCatalogSources.Srd, name.ToLowerInvariant(), new ItemTemplateData
    {
        Name = name,
        Category = ItemCategory.MagicItem,
        Subcategory = "Ring",
        Rarity = ItemRarity.Rare,
        RequiresAttunement = true,
    }, Now);

    public static EffectiveItem Effective(ItemTemplate? template, ItemOverrides? overrides = null) =>
        EffectiveItem.Resolve(template, overrides ?? ItemOverrides.None());
}
