using OpenTrpg.Core.Domain.Characters;
using OpenTrpg.Core.Domain.Common;
using OpenTrpg.Systems.Dnd5e.Domain.Catalog;
using OpenTrpg.Systems.Dnd5e.Domain.Characters;
using static OpenTrpg.Systems.Dnd5e.Domain.Tests.Characters.TestCatalog;

namespace OpenTrpg.Systems.Dnd5e.Domain.Tests.Characters;

/// <summary>Domain pieces of the sheet rules of phase 35B2 (formulas, rests, death saves, weapon rules, grants).</summary>
public class SheetRulesTests
{
    private static readonly ModifierFormula.Context Context = new(ProficiencyBonus: 3, ClassLevel: 5, CharacterLevel: 7, AbilityModifier: a => a == "wis" ? 2 : 0, WalkingSpeed: 25);

    [Fact]
    public void Formula_parts_say_where_each_point_comes_from()
    {
        var parts = ModifierFormula.Parts("characterLevel + 2*mod:wis + 1", "Robusto (nivel 4)", Context)!;

        Assert.Equal(
            [("Robusto (nivel 4): nivel de personaje", 7), ("Robusto (nivel 4): 2 × Sabiduría", 4), ("Robusto (nivel 4)", 1)],
            parts.Select(p => (p.Label, p.Value)).ToList());
        Assert.All(parts, p => Assert.Equal(BreakdownSources.Feature, p.Source));
        Assert.Equal(25, ModifierFormula.Parts("walk", "Aletas", Context)!.Sum(p => p.Value));
    }

    [Theory]
    [InlineData("characterLevel", false, true)]
    [InlineData("halfCharacterLevel + proficiencyBonus", false, true)]
    [InlineData("walk", false, false)]
    [InlineData("walk", true, true)]
    [InlineData("level + 2", false, false)]
    [InlineData("", false, false)]
    public void Formulas_follow_the_grammar(string formula, bool allowWalk, bool valid) =>
        Assert.Equal(valid, ModifierFormula.Parse(formula, allowWalk) is not null);

    [Fact]
    public void A_long_rest_can_recover_every_hit_die()
    {
        var character = ActiveCharacter([new ClassEntry("fighter", null, 4)], out var sheet);
        character.ShortRest(new Dictionary<string, int> { ["fighter"] = 4 }, sheet, new FixedDice(1), Now);

        character.LongRest(sheet.HitPointsMax, Now, allHitDice: true);

        Assert.Equal(4, character.HitDiceRemaining("fighter"));
    }

    [Fact]
    public void The_death_save_threshold_limits_the_failures()
    {
        var character = ActiveCharacter([new ClassEntry("fighter", null, 1)], out var sheet);

        Assert.Throws<DomainException>(() => { character.ApplyCombatUpdate(new CombatUpdate { DeathSaveFailures = 4 }, sheet.HitPointsMax, Now); });
        character.ApplyCombatUpdate(new CombatUpdate { DeathSaveFailures = 4 }, sheet.HitPointsMax, Now, deathSaveFailuresToDie: 4);

        Assert.Equal(4, character.DeathSaveFailures);
    }

    [Fact]
    public void Weapon_rules_are_read_from_the_system_data()
    {
        const string json = """
            {"addAbilityToDamage":true,"requires":{"str":15,"luck":3},"attunementClasses":["vigia-ejemplo"],
             "firearm":{"misfire":2,"era":"renaissance"},
             "alternate":{"name":"Culata","damage":"1d4","damageType":"bludgeoning","properties":["light"]}}
            """;

        var rules = WeaponRules.Parse(json);

        Assert.True(rules.AddAbilityToDamage);
        Assert.Equal(new Dictionary<string, int> { ["str"] = 15 }, rules.Requires);
        Assert.Equal(["vigia-ejemplo"], rules.AttunementClasses);
        Assert.Equal("renaissance", rules.FirearmEra);
        Assert.Equal(("Culata", "1d4", "bludgeoning"), (rules.Alternate!.Name, rules.Alternate.Damage, rules.Alternate.DamageType));
        Assert.Equal(["Requiere Fuerza 15; tienes 12."], rules.UnmetRequirements(_ => 12, _ => "Fuerza").ToList());
        Assert.Same(WeaponRules.None, WeaponRules.Parse("not json"));
    }

    [Fact]
    public void Grants_parse_defenses_size_saves_and_natural_weapons()
    {
        const string json = """
            {"resistances":["fire"],"immunities":["poison"],"conditionImmunities":["charmed"],"size":"Small",
             "removeSavingThrows":{"choose":1,"from":["str"]},"skillChoicesDelta":-1,
             "naturalWeapons":[{"name":"Garras","damage":"1d4","damageByCharacterLevel":{"5":"1d6","11":"1d8"},"damageType":"slashing","finesse":true}]}
            """;

        var grants = LevelChoiceJson.ParseGrants(json);

        Assert.Equal(["fire"], grants.Resistances);
        Assert.Equal(["poison"], grants.Immunities);
        Assert.Equal(["charmed"], grants.ConditionImmunities);
        Assert.Equal(("small", -1), (grants.Size, grants.SkillChoicesDelta));
        Assert.Equal(["str"], grants.RemoveSavingThrows!.Fixed);
        Assert.Empty(new RemovedSavingThrows(1, ["str", "con"]).Fixed);
        var claws = Assert.Single(grants.NaturalWeapons);
        Assert.Equal(("1d4", "1d6", "1d8"), (claws.DamageAt(1), claws.DamageAt(7), claws.DamageAt(20)));
        Assert.True(grants.HasSheetEffects);
    }

    [Fact]
    public void An_ability_maximum_feature_caps_the_score_at_its_value_instead_of_20()
    {
        var hardy = new FeatureDefinition
        {
            Index = "pack-hardy",
            Name = "Robustez de ejemplo",
            ClassIndex = "fighter",
            Level = 1,
            ModifiersJson = """[{"kind":"AbilityMax","target":"con","value":22}]""",
        };
        var character = NewCharacter(Scores(con: 24), [new ClassEntry("fighter", null, 1)]);

        var plain = SheetCalculator.Calculate(new SheetInput(character, Classes, null, null, Skills, EquippedGear.None, ChoiceEffects.None));
        var capped = SheetCalculator.Calculate(new SheetInput(character, Classes, null, null, Skills, EquippedGear.None, ChoiceEffects.None with
        {
            Modifiers = ChoiceEffects.FeatureModifiers(character, [hardy]),
        }));

        Assert.Equal(20, plain.AbilityMaxScores["con"]);
        Assert.Equal(22, capped.AbilityMaxScores["con"]);
        Assert.True(capped.Abilities["con"].Score <= 22);
        Assert.Equal(20, capped.AbilityMaxScores["str"]);
    }

    private static Dnd5eCharacter ActiveCharacter(IReadOnlyList<ClassEntry> classes, out CharacterSheet sheet)
    {
        var character = NewCharacter(Scores(con: 12), classes);
        sheet = Sheet(character);
        character.Activate(sheet.HitPointsMax, Now);
        return character;
    }
}
