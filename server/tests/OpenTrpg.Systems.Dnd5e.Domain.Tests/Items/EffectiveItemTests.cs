using OpenTrpg.Core.Domain.Catalog;
using OpenTrpg.Core.Domain.Items;
using static OpenTrpg.Systems.Dnd5e.Domain.Tests.Items.TestItems;
using OpenTrpg.Systems.Dnd5e.Domain.Catalog;
using OpenTrpg.Systems.Dnd5e.Domain.Characters;

namespace OpenTrpg.Systems.Dnd5e.Domain.Tests.Items;

public class EffectiveItemTests
{
    [Fact]
    public void Template_without_overrides_is_copied_and_is_not_custom()
    {
        var item = Effective(Longsword);

        Assert.Equal(("Longsword", ItemCategory.Weapon, "Martial Melee"), (item.Name, item.Category, item.Subcategory));
        Assert.Equal(("1d8", "Slashing", "1d10"), (item.DamageDice, item.DamageType, item.VersatileDice));
        Assert.Equal(["versatile"], item.Properties);
        Assert.Equal(3m, item.WeightLb);
        Assert.Equal((0, 0), (item.AttackBonus, item.DamageBonus));
        Assert.Empty(item.Effects);
        Assert.False(item.IsCustom);
        Assert.True(item.IsEquippable);
    }

    [Fact]
    public void Overrides_replace_only_the_fields_they_define()
    {
        var overrides = new ItemOverrides
        {
            Name = "Longsword +1",
            Rarity = ItemRarity.Uncommon,
            AttackBonus = 1,
            DamageBonus = 1,
            Effects = ["+1 a ataque y daño"],
        };

        var item = Effective(Longsword, overrides);

        Assert.Equal("Longsword +1", item.Name);
        Assert.Equal(ItemRarity.Uncommon, item.Rarity);
        Assert.Equal((1, 1), (item.AttackBonus, item.DamageBonus));
        Assert.Equal(["+1 a ataque y daño"], item.Effects);
        // Not overridden: kept from the template.
        Assert.Equal(("1d8", "Slashing", ItemCategory.Weapon), (item.DamageDice, item.DamageType, item.Category));
        Assert.Equal(["A sword."], item.Description);
        Assert.Equal(3m, item.WeightLb);
        Assert.True(item.IsCustom);
    }

    [Fact]
    public void Without_template_every_field_comes_from_the_overrides()
    {
        var item = Effective(null, new ItemOverrides { Name = "Amuleto familiar", Category = ItemCategory.MagicItem, RequiresAttunement = true, WeightLb = 0.5m });

        Assert.Equal(("Amuleto familiar", ItemCategory.MagicItem, true, 0.5m), (item.Name, item.Category, item.RequiresAttunement, item.WeightLb));
        Assert.Null(item.Subcategory);
        Assert.Null(item.DamageDice);
        Assert.True(item.IsCustom);
    }

    [Fact]
    public void Without_template_or_name_defaults_are_used()
    {
        var item = Effective(null);

        Assert.Equal((EffectiveItem.UnnamedItem, ItemCategory.Other), (item.Name, item.Category));
        Assert.True(item.IsCustom);
    }

    [Fact]
    public void Armor_overrides_change_the_armor_values()
    {
        var item = Effective(ChainMail, new ItemOverrides { ArmorClassBase = 17, StealthDisadvantage = false });

        Assert.Equal((17, false, 13), (item.ArmorClassBase, item.StealthDisadvantage, item.StrengthMinimum));
        Assert.False(item.AddDexModifier);
    }

    [Fact]
    public void Consumables_and_stackables_follow_category_and_subcategory()
    {
        Assert.True(Effective(Healing).IsConsumable);
        Assert.True(Effective(Arrow).IsConsumable);
        Assert.True(Effective(Rope).IsStackable);
        Assert.False(Effective(Rope).IsConsumable);
        Assert.False(Effective(Longsword).IsStackable);
        Assert.True(Effective(null, new ItemOverrides { Name = "Bomba", Category = ItemCategory.Consumable }).IsConsumable);
    }

    [Fact]
    public void Normalize_trims_and_drops_blank_values()
    {
        var normalized = new ItemOverrides { Name = "  Daga  ", DamageDice = " ", Effects = [" ", ""], Properties = [" light "] }.Normalize();

        Assert.Equal("Daga", normalized.Name);
        Assert.Null(normalized.DamageDice);
        Assert.Null(normalized.Effects);
        Assert.Equal(["light"], normalized.Properties);
        Assert.True(new ItemOverrides { Name = " ", Effects = [] }.Normalize().IsEmpty);
    }

    [Theory]
    [InlineData(31, 0)]
    [InlineData(0, 21)]
    public void Normalize_rejects_out_of_range_values(int armorClass, int attackBonus) =>
        Assert.Throws<OpenTrpg.Core.Domain.Common.DomainException>(() => new ItemOverrides { ArmorClassBase = armorClass, AttackBonus = attackBonus }.Normalize());

    [Fact]
    public void Equipped_gear_uses_the_effective_armor_and_shield()
    {
        var gear = OpenTrpg.Systems.Dnd5e.Domain.Characters.EquippedGear.FromEquipped([Effective(ChainMail), Effective(Shield), Effective(Longsword)]);

        Assert.Equal((16, false, true), (gear.ArmorClassBase, gear.AddDexModifier, gear.HasShield));
    }
}
