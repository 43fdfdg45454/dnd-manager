using OpenTrpg.Core.Domain.Catalog;
using OpenTrpg.Core.Domain.Characters;
using OpenTrpg.Core.Domain.Common;
using static OpenTrpg.Systems.Dnd5e.Domain.Tests.Characters.TestCatalog;
using OpenTrpg.Systems.Dnd5e.Domain.Catalog;
using OpenTrpg.Systems.Dnd5e.Domain.Characters;

namespace OpenTrpg.Systems.Dnd5e.Domain.Tests.Characters;

/// <summary>Animal companion: the rule of the feature, the recalculated statblock and its hit points (phase 25, block 6).</summary>
public class CompanionTests
{
    private static readonly CompanionRule Rule = new(0.25, ["Small", "Medium"], 4, true, true);

    /// <summary>A wolf-like beast: AC 13, 11 HP, Perception +3, Stealth +4, Bite +4 (2d4+2) and a multiattack.</summary>
    private static readonly CompanionBeast Wolf = new(
        "lobo-ejemplo",
        "Lobo de ejemplo",
        "Medium",
        0.25,
        13,
        11,
        new Dictionary<string, int> { ["dex"] = 4 },
        new Dictionary<string, int> { ["perception"] = 3, ["stealth"] = 4 },
        [
            new CompanionBeastAction("Multiattack", "Two bites.", null, [], true),
            new CompanionBeastAction("Bite", "Melee Weapon Attack.", 4, [new CompanionDamage("2d4+2", "Piercing"), new CompanionDamage("1d6", "Poison")], false),
            new CompanionBeastAction("Howl", "Frightens.", null, [], false),
        ]);

    [Fact]
    public void The_character_proficiency_bonus_adds_to_armor_class_saves_and_skills_with_breakdowns()
    {
        var block = CompanionCalculator.Calculate(Wolf, Rule, "ranger", 3, 2);

        Assert.Equal(15, block.ArmorClass.Total);
        Assert.Equal(
            [("base", CompanionCalculator.BeastArmorClassLabel, 13), ("proficiency", CompanionCalculator.CharacterProficiencyLabel, 2)],
            block.ArmorClass.Parts.Select(p => (p.Source, p.Label, p.Value)));
        Assert.Equal(6, block.SavingThrows["dex"].Total);
        Assert.Equal(5, block.Skills["perception"].Total);
        Assert.Equal(6, block.Skills["stealth"].Total);
        Assert.Equal(2, block.Skills.Count);
    }

    [Fact]
    public void Attacks_add_the_proficiency_bonus_to_attack_and_to_the_first_damage_component()
    {
        var block = CompanionCalculator.Calculate(Wolf, Rule, "ranger", 5, 3);

        var bite = Assert.Single(block.Attacks, a => a.Name == "Bite");
        Assert.Equal(7, bite.AttackBonus!.Total);
        Assert.Equal([4, 3], bite.AttackBonus.Parts.Select(p => p.Value));
        Assert.Equal(["2d4+5", "1d6"], bite.Damage.Select(d => d.Dice));
        Assert.Equal(5, bite.DamageBonus!.Total);
        Assert.Equal(
            [("base", CompanionCalculator.BeastDamageLabel, 2), ("proficiency", CompanionCalculator.CharacterProficiencyLabel, 3)],
            bite.DamageBonus.Parts.Select(p => (p.Source, p.Label, p.Value)));

        var multiattack = Assert.Single(block.Attacks, a => a.IsMultiattack);
        Assert.Null(multiattack.AttackBonus);
        Assert.Null(multiattack.DamageBonus);
    }

    [Theory]
    [InlineData(2, 11)]
    [InlineData(3, 12)]
    [InlineData(10, 40)]
    public void Maximum_hit_points_are_the_higher_of_the_beast_and_four_per_level(int level, int expected)
    {
        var block = CompanionCalculator.Calculate(Wolf, Rule, "ranger", level, 2);

        Assert.Equal(expected, block.HitPointsMax.Total);
        Assert.Equal(expected, block.HitPointsMax.Parts.Sum(p => p.Value));
        if (expected > 11)
        {
            Assert.Equal(("class", $"4 × Nivel de explorador ({level})"), (block.HitPointsMax.Parts[1].Source, block.HitPointsMax.Parts[1].Label));
        }
        else
        {
            Assert.Single(block.HitPointsMax.Parts);
        }
    }

    [Fact]
    public void Without_the_flags_the_beast_keeps_its_own_values()
    {
        var plain = new CompanionRule(1, [], null, false, false);

        var block = CompanionCalculator.Calculate(Wolf, plain, "ranger", 10, 4);

        Assert.Equal(13, block.ArmorClass.Total);
        Assert.Equal(11, block.HitPointsMax.Total);
        Assert.Equal(3, block.Skills["perception"].Total);
        var bite = block.Attacks.Single(a => a.Name == "Bite");
        Assert.Equal((4, "2d4+2"), (bite.AttackBonus!.Total, bite.Damage[0].Dice));
    }

    [Fact]
    public void A_manual_maximum_is_a_final_part()
    {
        var block = CompanionCalculator.Calculate(Wolf, Rule, "ranger", 3, 2, hitPointsMaxOverride: 20);

        Assert.Equal(20, block.HitPointsMax.Total);
        Assert.Equal(("override", 8), (block.HitPointsMax.Parts[^1].Source, block.HitPointsMax.Parts[^1].Value));
    }

    [Theory]
    [InlineData("2d4+2", "2d4", 2)]
    [InlineData("1d6", "1d6", 0)]
    [InlineData("1d10 - 1", "1d10", -1)]
    [InlineData("special", "special", 0)]
    public void Damage_dice_split_into_dice_and_flat_bonus(string text, string dice, int flat)
    {
        Assert.Equal((dice, flat), CompanionCalculator.SplitDice(text));
    }

    [Theory]
    [InlineData("beast", true, null)]
    [InlineData("max(beast, 4*classLevel)", true, 4)]
    [InlineData("max(beast,20*classLevel)", true, 20)]
    [InlineData("max(beast, 0*classLevel)", false, null)]
    [InlineData("max(beast, 21*classLevel)", false, null)]
    [InlineData("4*classLevel", false, null)]
    [InlineData("", false, null)]
    public void Hit_points_texts_are_beast_or_a_maximum_with_the_class_level(string text, bool valid, int? perLevel)
    {
        Assert.Equal(valid, CompanionRule.TryParseHitPoints(text, out var parsed));
        Assert.Equal(perLevel, parsed);
    }

    [Fact]
    public void The_rule_round_trips_through_its_json_and_filters_beasts()
    {
        var parsed = CompanionRule.Parse(Rule.ToJson())!;

        Assert.Equal((0.25, 4, true, true), (parsed.MaxChallengeRating, parsed.HitPointsPerClassLevel!.Value, parsed.ProficiencyBonusFromCharacter, parsed.AttackBonusFromCharacter));
        Assert.Equal(["Small", "Medium"], parsed.Sizes);
        Assert.True(parsed.Allows(0.25, "Medium"));
        Assert.False(parsed.Allows(0.5, "Medium"));
        Assert.False(parsed.Allows(0.125, "Large"));
        Assert.True(new CompanionRule(1, [], null, false, false).Allows(1, "Huge"));
    }

    [Fact]
    public void The_grant_needs_the_subclass_and_the_level_of_the_feature()
    {
        FeatureDefinition[] features =
        [
            new() { Index = "vinculo", Name = "Vínculo", ClassIndex = "ranger", SubclassIndex = "guardabosques-ejemplo", Level = 3, CompanionJson = Rule.ToJson() },
            new() { Index = "otro", Name = "Otro", ClassIndex = "ranger", SubclassIndex = "guardabosques-ejemplo", Level = 3 },
        ];

        Assert.Null(CompanionGrants.Find(NewCharacter(Scores(), [new ClassEntry("ranger", "guardabosques-ejemplo", 2)]), features));
        Assert.Null(CompanionGrants.Find(NewCharacter(Scores(), [new ClassEntry("ranger", "otro-ejemplo", 5)]), features));
        var grant = CompanionGrants.Find(NewCharacter(Scores(), [new ClassEntry("ranger", "guardabosques-ejemplo", 5)]), features)!;
        Assert.Equal(("vinculo", "ranger", 5), (grant.Feature.Index, grant.ClassIndex, grant.ClassLevel));
    }

    [Fact]
    public void Hit_points_are_tracked_between_zero_and_the_maximum()
    {
        var companion = CharacterCompanion.Create(Guid.NewGuid(), "lobo-ejemplo", "  Ceniza ", 12, Now);
        Assert.Equal(("Ceniza", 12), (companion.Name, companion.HitPointsCurrent));

        companion.TrackHitPoints(-5, null, 12, Now);
        Assert.Equal(7, companion.HitPointsCurrent);
        companion.TrackHitPoints(-50, null, 12, Now);
        Assert.Equal(0, companion.HitPointsCurrent);
        companion.TrackHitPoints(30, null, 12, Now);
        Assert.Equal(12, companion.HitPointsCurrent);
        companion.TrackHitPoints(null, 4, 12, Now);
        Assert.Equal(4, companion.HitPointsCurrent);

        Assert.Throws<DomainException>(() => companion.TrackHitPoints(null, 13, 12, Now));
        Assert.Throws<DomainException>(() => companion.TrackHitPoints(1, 1, 12, Now));
        Assert.Throws<DomainException>(() => companion.Rename(" ", Now));

        companion.ChangeBeast("pantera-ejemplo", "Sombra", 13, Now);
        Assert.Equal(("pantera-ejemplo", "Sombra", 13), (companion.BeastIndex, companion.Name, companion.HitPointsCurrent));
        Assert.Equal(10, companion.CurrentWithin(10));
    }
}
