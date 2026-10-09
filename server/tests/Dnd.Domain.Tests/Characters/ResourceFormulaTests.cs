using Dnd.Domain.Catalog;
using Dnd.Domain.Characters;
using Dnd.Domain.Items;
using Dnd.Domain.Tests.Items;
using static Dnd.Domain.Tests.Characters.TestCatalog;

namespace Dnd.Domain.Tests.Characters;

/// <summary>Maximum formulas, by-level tables and dice of pack resources, and resources of subclass features (phase 25, block 2).</summary>
public class ResourceFormulaTests
{
    private static int Mods(string ability) => ability switch
    {
        "int" => 3,
        "wis" => -1,
        _ => 0,
    };

    private static OptionResource Parse(string json) => LevelChoiceJson.ParseResource(json)!;

    [Theory]
    [InlineData("2*classLevel+mod:int", 5, 13)]
    [InlineData("classLevel+mod:wis", 5, 4)]
    [InlineData("proficiencyBonus", 5, 3)]
    [InlineData("halfClassLevel", 5, 2)]
    [InlineData("mod:int", 5, 3)]
    [InlineData("3", 5, 3)]
    [InlineData("proficiencyBonus + 2 + 3*halfClassLevel", 9, 18)]
    public void Compound_formulas_add_their_terms(string formula, int classLevel, int expected)
    {
        var resource = Parse($$"""{"key":"k","name":"Ward","max":"{{formula}}"}""");

        Assert.Equal(expected, resource.Evaluate(classLevel <= 4 ? 2 : classLevel <= 8 ? 3 : 4, classLevel, Mods));
    }

    [Fact]
    public void The_breakdown_lists_every_term()
    {
        var resource = Parse("""{"key":"ward","name":"Arcane Shield","max":"2*classLevel+mod:int+1"}""");

        var breakdown = resource.Calculate(3, 6, Mods, "Arcane Shield (nivel 2)")!;

        Assert.Equal(16, breakdown.Total);
        Assert.Equal(
            "16 = class:2 × Nivel de clase 12, ability:Inteligencia 3, feature:Arcane Shield (nivel 2) 1",
            ItemModifierTests.Text(breakdown));
    }

    [Fact]
    public void Formulas_are_raised_to_one_unless_min_says_otherwise()
    {
        var byDefault = Parse("""{"key":"k","name":"Calm","max":"mod:wis"}""");
        var explicitZero = Parse("""{"key":"k","name":"Calm","max":{"formula":"mod:wis","min":0}}""");
        var explicitTwo = Parse("""{"key":"k","name":"Calm","max":{"formula":"mod:wis","min":2}}""");

        var breakdown = byDefault.Calculate(2, 3, Mods)!;
        Assert.Equal(1, breakdown.Total);
        Assert.Equal("1 = ability:Sabiduría -1, base:Mínimo 1 2", ItemModifierTests.Text(breakdown));
        Assert.Equal(0, explicitZero.Evaluate(2, 3, Mods));
        Assert.Equal(2, explicitTwo.Evaluate(2, 3, Mods));
    }

    [Theory]
    [InlineData(2, null)]
    [InlineData(3, 4)]
    [InlineData(6, 4)]
    [InlineData(7, 5)]
    [InlineData(14, 5)]
    [InlineData(15, 6)]
    [InlineData(20, 6)]
    public void By_level_uses_the_highest_entry_not_above_the_class_level(int classLevel, int? expected)
    {
        var resource = Parse("""{"key":"tactics-dice","name":"Tactics Dice","max":{"byLevel":{"3":4,"7":5,"15":6}},"recharge":"ShortRest"}""");

        Assert.Equal(expected, resource.Calculate(2, classLevel, Mods)?.Total);
    }

    [Fact]
    public void By_level_breakdown_names_the_table_entry()
    {
        var resource = Parse("""{"key":"tactics-dice","name":"Tactics Dice","max":{"byLevel":{"3":4,"7":5}}}""");

        Assert.Equal(
            "5 = feature:Ventaja táctica (nivel 3), tabla desde el nivel 7 5",
            ItemModifierTests.Text(resource.Calculate(3, 9, Mods, "Ventaja táctica (nivel 3)")!));
    }

    [Theory]
    [InlineData(3, "d8")]
    [InlineData(9, "d8")]
    [InlineData(10, "d10")]
    [InlineData(18, "d12")]
    [InlineData(1, "d6")]
    public void Dice_by_level_overrides_the_base_die_from_each_level(int classLevel, string expected)
    {
        var resource = Parse("""{"key":"k","name":"Tactics Dice","max":4,"dice":"d6","diceByLevel":{"3":"d8","10":"d10","18":"d12"}}""");

        Assert.Equal(expected, resource.DiceAt(classLevel));
    }

    [Fact]
    public void Resources_without_dice_have_none()
    {
        Assert.Null(Parse("""{"key":"k","name":"K","max":1}""").DiceAt(5));
        Assert.Equal("d8", Parse("""{"key":"k","name":"K","max":1,"diceByLevel":{"3":"d8"}}""").DiceAt(5));
        Assert.Null(Parse("""{"key":"k","name":"K","max":1,"diceByLevel":{"3":"d8"}}""").DiceAt(2));
    }

    [Theory]
    [InlineData("1", true)]
    [InlineData("999", true)]
    [InlineData("2*classLevel+mod:int", true)]
    [InlineData("classLevel + mod:wis", true)]
    [InlineData("0", false)]
    [InlineData("1000", false)]
    [InlineData("2*", false)]
    [InlineData("classLevel+", false)]
    [InlineData("classLevel-1", false)]
    [InlineData("mod:luck", false)]
    [InlineData("100*classLevel", false)]
    [InlineData("level", false)]
    [InlineData("classLevel*2", false)]
    public void The_grammar_is_checked(string formula, bool valid)
    {
        Assert.Equal(valid, OptionResource.IsValidMax(formula));
    }

    [Fact]
    public void A_constant_zero_is_only_valid_with_min_zero()
    {
        Assert.False(OptionResource.IsValidMax("0"));
        Assert.True(OptionResource.IsValidMax("0", min: 0));
    }

    [Fact]
    public void Subclass_feature_resources_appear_from_their_level_with_their_origin()
    {
        var feature = new FeatureDefinition
        {
            Index = "pack-tactical-edge",
            Name = "Ventaja táctica",
            ClassIndex = "fighter",
            SubclassIndex = "pack-tactician",
            Level = 3,
            ResourceJson = """{"key":"tactics-dice","name":"Tactics Dice","max":{"byLevel":{"3":4,"7":5}},"recharge":"ShortRest","dice":"d8","diceByLevel":{"10":"d10"}}""",
        };
        var other = new FeatureDefinition
        {
            Index = "pack-other-feature",
            Name = "Other",
            ClassIndex = "fighter",
            SubclassIndex = "pack-other",
            Level = 3,
            ResourceJson = """{"key":"other","name":"Other","max":1}""",
        };

        var below = NewCharacter(Scores(), [new ClassEntry("fighter", "pack-tactician", 2)]);
        Assert.Empty(ChoiceEffects.FeatureResources(below, [feature, other]));

        var character = NewCharacter(Scores(), [new ClassEntry("fighter", "pack-tactician", 10)]);
        var effects = ChoiceEffects.FeatureResources(character, [feature, other]);
        var sheet = SheetCalculator.Calculate(new SheetInput(character, Classes, null, null, Skills, EquippedGear.None, ChoiceEffects.None with { Resources = effects }));

        var template = Assert.Single(sheet.ChoiceResources);
        Assert.Equal(("tactics-dice", 5, ResourceRecharge.ShortRest, "d10", "Ventaja táctica (nivel 3)"), (template.Key, template.Max, template.Recharge, template.Dice, template.Source));
        Assert.Equal("5 = feature:Ventaja táctica (nivel 3), tabla desde el nivel 7 5", ItemModifierTests.Text(template.Breakdown!));

        character.SyncAutoResources([.. ClassResourceRules.ForClasses(character.Classes, sheet.AbilityModifiers), .. sheet.ChoiceResources]);
        var resource = Assert.Single(character.Resources, r => r.Key == "tactics-dice");
        Assert.Equal((5, true), (resource.Max, resource.IsAuto));
    }

    [Theory]
    [InlineData(1, "d6")]
    [InlineData(5, "d8")]
    [InlineData(10, "d10")]
    [InlineData(15, "d12")]
    public void Bardic_inspiration_carries_its_die(int level, string die)
    {
        var template = Assert.Single(ClassResourceRules.For("bard", level, new Dictionary<string, int> { ["cha"] = 3 }));

        Assert.Equal(("bardic-inspiration", 3, die), (template.Key, template.Max, template.Dice));
    }
}
