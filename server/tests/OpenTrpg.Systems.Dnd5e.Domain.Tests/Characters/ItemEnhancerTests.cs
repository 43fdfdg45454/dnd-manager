using OpenTrpg.Core.Domain.Catalog;
using OpenTrpg.Core.Domain.Items;
using OpenTrpg.Systems.Dnd5e.Domain.Catalog;
using OpenTrpg.Systems.Dnd5e.Domain.Characters;

namespace OpenTrpg.Systems.Dnd5e.Domain.Tests.Characters;

/// <summary>Enhancements of a concrete item (phase 35B3, H31).</summary>
public class ItemEnhancerTests
{
    private static readonly EffectiveItem Sword = new()
    {
        Name = "Espada de ejemplo",
        Category = ItemCategory.Weapon,
        DamageDice = "1d8",
        VersatileDice = "1d10",
        DamageType = "slashing",
        Properties = ["versatile"],
    };

    [Theory]
    [InlineData("1d8", 1, "1d10")]
    [InlineData("1d12", 1, "2d8")]
    [InlineData("2d6", 1, "2d8")]
    [InlineData("1d4", -3, "1")]
    [InlineData("2d12", 2, "2d12")]
    [InlineData("3d6", 1, "3d6")]
    public void Damage_steps_climb_the_ladder_within_its_ends(string dice, int steps, string expected) =>
        Assert.Equal(expected, ItemEnhancer.StepDamage(dice, steps));

    [Fact]
    public void Enhancements_change_the_item_and_label_their_bonuses_with_the_option()
    {
        var keen = new AppliedEnhancement("filo", "Filo", new ItemEffects { AttackBonus = 1, DamageStep = 1, AddProperties = ["finesse", "versatile"] }, []);
        var flame = new AppliedEnhancement("llama", "Llama", new ItemEffects { AppliesTo = [ItemEffects.Weapon], DamageType = "fire" }, [new ItemModifier(ItemModifierKind.SaveBonus, "dex", 1)]);
        var armorOnly = new AppliedEnhancement("placa", "Placa", new ItemEffects { AppliesTo = [ItemEffects.Armor], ArmorClassBonus = 2 }, []);

        var enhanced = ItemEnhancer.Apply(Sword, [keen, flame, armorOnly]);

        Assert.Equal(("1d10", "1d12", "fire"), (enhanced.Item.DamageDice, enhanced.Item.VersatileDice, enhanced.Item.DamageType));
        Assert.Equal(["versatile", "finesse"], enhanced.Item.Properties);
        Assert.Equal(["filo", "llama"], enhanced.Enhancements.Select(e => e.Index));
        Assert.Equal(
            [("Filo (Espada de ejemplo)", ItemModifierKind.AttackBonus, 1), ("Llama (Espada de ejemplo)", ItemModifierKind.SaveBonus, 1)],
            enhanced.Bonuses.Select(b => (b.Label, b.Modifier.Kind, b.Modifier.Value)));
        Assert.Same(Sword, ItemEnhancer.Apply(Sword, [armorOnly]).Item);
    }

    [Fact]
    public void The_rule_gives_the_options_per_item_of_the_highest_level_reached()
    {
        var rule = ItemEnhancementRule.Parse(new ItemEnhancementRule(["mejoras-ejemplo"], new Dictionary<int, int> { [2] = 1, [6] = 3 }).ToJson())!;
        Assert.Equal((0, 1, 1, 3), (rule.MaxPerItemAt(1), rule.MaxPerItemAt(2), rule.MaxPerItemAt(5), rule.MaxPerItemAt(20)));
        Assert.Null(ItemEnhancementRule.Parse("{\"optionSets\":[]}"));
        Assert.Equal(-1, ItemEffects.Parse(new ItemEffects { DamageStep = -1 }.ToJson())!.DamageStep);
    }
}
