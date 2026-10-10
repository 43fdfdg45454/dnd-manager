using OpenTrpg.Core.Domain.Catalog;
using OpenTrpg.Core.Domain.Characters;
using OpenTrpg.Core.Domain.Tests.Items;
using static OpenTrpg.Core.Domain.Tests.Characters.TestCatalog;

namespace OpenTrpg.Core.Domain.Tests.Characters;

/// <summary>Sheet modifiers of subclass features from content packs (phase 25, block 7).</summary>
public class FeatureModifierTests
{
    private static readonly FeatureDefinition Swift = new()
    {
        Index = "pack-swift-steps",
        Name = "Pasos veloces",
        ClassIndex = "fighter",
        SubclassIndex = "pack-skirmisher",
        Level = 3,
        ModifiersJson = """[{"kind":"InitiativeBonus","target":null,"value":2},{"kind":"SpeedBonus","target":null,"value":10},{"kind":"ArmorClassBonus","target":null,"value":1}]""",
    };

    private static readonly FeatureDefinition Guarded = new()
    {
        Index = "pack-guarded",
        Name = "Guardia de ejemplo",
        ClassIndex = "fighter",
        SubclassIndex = "pack-skirmisher",
        Level = 7,
        ModifiersJson = """[{"kind":"ArmorClassBonus","target":null,"value":1,"condition":"wearingArmor"}]""",
    };

    private static CharacterSheet SheetWith(Dnd5eCharacter character, EquippedGear gear)
    {
        var modifiers = ChoiceEffects.FeatureModifiers(character, [Swift, Guarded]);
        return SheetCalculator.Calculate(new SheetInput(character, Classes, null, null, Skills, gear, ChoiceEffects.None with { Modifiers = modifiers }));
    }

    [Fact]
    public void Unconditional_modifiers_add_to_armor_class_initiative_and_speed_with_the_feature_as_label()
    {
        var character = NewCharacter(Scores(dex: 14), [new ClassEntry("fighter", "pack-skirmisher", 3)]);

        var sheet = SheetWith(character, EquippedGear.None);

        Assert.Equal((13, 4, 40), (sheet.ArmorClass, sheet.Initiative, sheet.Speed));
        Assert.Contains(sheet.Breakdowns["initiative"].Parts, p => (p.Source, p.Label, p.Value) == (BreakdownSources.Feature, "Pasos veloces (nivel 3)", 2));
        Assert.Contains(sheet.Breakdowns["speed"].Parts, p => (p.Source, p.Label, p.Value) == (BreakdownSources.Feature, "Pasos veloces (nivel 3)", 10));
        Assert.Contains(sheet.Breakdowns["armorClass"].Parts, p => (p.Source, p.Label, p.Value) == (BreakdownSources.Feature, "Pasos veloces (nivel 3)", 1));
        Assert.DoesNotContain(sheet.Breakdowns["armorClass"].Parts, p => p.Label.StartsWith("Guardia", StringComparison.Ordinal));
    }

    [Fact]
    public void Wearing_armor_modifiers_apply_only_with_armor()
    {
        var character = NewCharacter(Scores(dex: 14), [new ClassEntry("fighter", "pack-skirmisher", 7)]);

        var armored = SheetWith(character, EquippedGear.FromEquipped([TestItems.Effective(TestItems.ChainMail)]));
        var unarmored = SheetWith(character, EquippedGear.None);

        Assert.Equal(18, armored.ArmorClass);
        Assert.Contains(armored.Breakdowns["armorClass"].Parts, p => (p.Label, p.Value) == ("Guardia de ejemplo (nivel 7)", 1));
        Assert.Equal(13, unarmored.ArmorClass);
        Assert.DoesNotContain(unarmored.Breakdowns["armorClass"].Parts, p => p.Label == "Guardia de ejemplo (nivel 7)");
    }

    [Fact]
    public void Features_above_the_class_level_do_not_apply()
    {
        var character = NewCharacter(Scores(dex: 14), [new ClassEntry("fighter", "pack-skirmisher", 2)]);

        Assert.Empty(ChoiceEffects.FeatureModifiers(character, [Swift, Guarded]));
        var sheet = SheetWith(character, EquippedGear.None);
        Assert.Equal((12, 2, 30), (sheet.ArmorClass, sheet.Initiative, sheet.Speed));
    }

    [Fact]
    public void Changing_subclass_drops_the_modifiers()
    {
        var character = NewCharacter(Scores(dex: 14), [new ClassEntry("fighter", "pack-skirmisher", 7)]);
        Assert.Equal(4, ChoiceEffects.FeatureModifiers(character, [Swift, Guarded]).Count);

        character.ReplaceClasses([new ClassEntry("fighter", "champion", 7)], Now);

        Assert.Empty(ChoiceEffects.FeatureModifiers(character, [Swift, Guarded]));
        Assert.Equal(2, SheetWith(character, EquippedGear.None).Initiative);
    }

    [Fact]
    public void Features_without_modifiers_parse_as_empty()
    {
        var plain = new FeatureDefinition { Index = "pack-plain", Name = "Sin efectos", ClassIndex = "fighter", SubclassIndex = "pack-skirmisher", Level = 1 };

        Assert.Empty(plain.Modifiers);
    }
}
