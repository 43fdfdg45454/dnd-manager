using OpenTrpg.Core.Domain.Catalog;
using OpenTrpg.Core.Domain.Characters;
using OpenTrpg.Core.Domain.Common;
using OpenTrpg.Core.Domain.Items;
using OpenTrpg.Systems.Dnd5e.Domain.Tests.Items;
using static OpenTrpg.Systems.Dnd5e.Domain.Tests.Characters.TestCatalog;
using OpenTrpg.Systems.Dnd5e.Domain.Catalog;
using OpenTrpg.Systems.Dnd5e.Domain.Characters;

namespace OpenTrpg.Systems.Dnd5e.Domain.Tests.Characters;

/// <summary>Effects of the level choices on the sheet and the attacks (phase 16c).</summary>
public class LevelChoiceTests
{
    private static readonly OptionDefinition Defense = Option(
        "fighting-style-defense", "Defense", """[{"kind":"ArmorClassBonus","target":null,"value":1,"condition":"wearingArmor"}]""");

    private static readonly OptionDefinition Archery = Option(
        "fighting-style-archery", "Archery", """[{"kind":"AttackBonus","target":null,"value":2,"condition":"rangedWeapon"}]""");

    private static readonly OptionDefinition Dueling = Option(
        "fighting-style-dueling", "Dueling", """[{"kind":"DamageBonus","target":null,"value":2,"condition":"oneHandedMeleeNoOtherWeapon"}]""");

    private static readonly OptionDefinition Resilient = new()
    {
        Index = "pack-resilient",
        SetId = OptionSets.Feats,
        Name = "Resilient",
        AbilityIncreaseJson = """{"amount":1,"from":[]}""",
    };

    private static readonly OptionDefinition DreadfulWord = new()
    {
        Index = "eldritch-invocation-dreadful-word",
        SetId = "eldritch-invocations",
        Name = "Dreadful Word",
        ResourceJson = """{"key":"dreadful-word","name":"Dreadful Word","max":1,"recharge":"LongRest"}""",
    };

    private static readonly ItemTemplate Shortbow = ItemTemplate.CreateCatalog(Dnd5eCatalogSources.Srd, "shortbow", new ItemTemplateData
    {
        Name = "Shortbow",
        Category = ItemCategory.Weapon,
        Subcategory = "Simple Ranged",
        DamageDice = "1d6",
        DamageType = "Piercing",
        Properties = ["ammunition", "range", "two-handed"],
        RangeNormal = 80,
        RangeLong = 320,
    }, Now);

    private static readonly ItemTemplate Dagger = ItemTemplate.CreateCatalog(Dnd5eCatalogSources.Srd, "dagger", new ItemTemplateData
    {
        Name = "Dagger",
        Category = ItemCategory.Weapon,
        Subcategory = "Simple Melee",
        DamageDice = "1d4",
        DamageType = "Piercing",
        Properties = ["finesse", "light", "thrown"],
    }, Now);

    [Fact]
    public void Defense_adds_1_to_the_armor_class_only_while_wearing_armor()
    {
        var character = Fighter(Scores(dex: 14));
        Pick(character, "fighting-style", "OptionSet", "fighting-styles", Defense, level: 1);

        var armored = SheetWith(character, EquippedGear.FromEquipped([TestItems.Effective(TestItems.ChainMail)]));
        var unarmored = SheetWith(character, EquippedGear.None);

        Assert.Equal(17, armored.ArmorClass);
        Assert.Equal("17 = armor:Chain Mail 16, feature:Defense (nivel 1) 1", ItemModifierTests.Text(armored.Breakdowns["armorClass"]));
        Assert.Equal(12, unarmored.ArmorClass);
        Assert.DoesNotContain(unarmored.Breakdowns["armorClass"].Parts, p => p.Source == BreakdownSources.Feature);
    }

    [Fact]
    public void Ability_score_improvements_are_breakdown_parts_capped_at_20()
    {
        var character = Fighter(Scores(str: 19, dex: 14));
        character.RecordChoice(4, "fighter", "asi", new ChoiceSelection { Kind = "AsiOrFeat", Name = "ASI", Asi = new Dictionary<string, int> { ["str"] = 2 } }, Now);
        character.RecordChoice(8, "fighter", "asi", new ChoiceSelection { Kind = "AsiOrFeat", Name = "ASI", Asi = new Dictionary<string, int> { ["dex"] = 1, ["con"] = 1 } }, Now);

        var sheet = SheetWith(character, EquippedGear.None);

        Assert.Equal(20, sheet.Abilities["str"].Score);
        Assert.Equal(
            "20 = base:Puntuación base 19, feature:Mejora de característica (nivel 4) 2, feature:Límite de las mejoras (20) -1",
            ItemModifierTests.Text(sheet.Breakdowns["ability.str"]));
        Assert.Equal("15 = base:Puntuación base 14, feature:Mejora de característica (nivel 8) 1", ItemModifierTests.Text(sheet.Breakdowns["ability.dex"]));
        Assert.Equal(11, sheet.Abilities["con"].Score);
    }

    [Fact]
    public void A_feat_raises_the_chosen_ability_with_its_name_as_label()
    {
        var character = Fighter(Scores(wis: 12));
        character.RecordChoice(4, "fighter", "asi", new ChoiceSelection
        {
            Kind = "AsiOrFeat",
            Name = "ASI",
            SetId = OptionSets.Feats,
            Feat = new ChoiceItem(Resilient.Index, Resilient.Name),
            Ability = "wis",
        }, Now);

        var sheet = SheetWith(character, EquippedGear.None, Resilient);

        Assert.Equal("13 = base:Puntuación base 12, feature:Resilient (nivel 4) 1", ItemModifierTests.Text(sheet.Breakdowns["ability.wis"]));
    }

    [Fact]
    public void A_feat_with_a_fixed_ability_raises_it_without_a_chosen_ability()
    {
        var durable = new OptionDefinition
        {
            Index = "pack-durable",
            SetId = OptionSets.Feats,
            Name = "Durable",
            AbilityIncreaseJson = """{"amount":1,"from":["con"]}""",
        };
        var character = Fighter(Scores(con: 14));
        character.RecordChoice(4, "fighter", "asi", new ChoiceSelection
        {
            Kind = "AsiOrFeat",
            Name = "ASI",
            SetId = OptionSets.Feats,
            Feat = new ChoiceItem(durable.Index, durable.Name),
        }, Now);

        var sheet = SheetWith(character, EquippedGear.None, durable);

        Assert.Equal("15 = base:Puntuación base 14, feature:Durable (nivel 4) 1", ItemModifierTests.Text(sheet.Breakdowns["ability.con"]));
        Assert.Equal(10, sheet.Abilities["str"].Score);
    }

    [Fact]
    public void A_feat_that_offers_several_abilities_needs_the_chosen_one()
    {
        var athlete = new OptionDefinition
        {
            Index = "pack-athlete",
            SetId = OptionSets.Feats,
            Name = "Athlete",
            AbilityIncreaseJson = """{"amount":1,"from":["str","dex"]}""",
        };
        var character = Fighter(Scores(str: 15, dex: 13));
        character.RecordChoice(4, "fighter", "asi", new ChoiceSelection
        {
            Kind = "AsiOrFeat",
            Name = "ASI",
            SetId = OptionSets.Feats,
            Feat = new ChoiceItem(athlete.Index, athlete.Name),
            Ability = "dex",
        }, Now);

        var sheet = SheetWith(character, EquippedGear.None, athlete);

        Assert.Equal((15, 14), (sheet.Abilities["str"].Score, sheet.Abilities["dex"].Score));
        Assert.Equal("14 = base:Puntuación base 13, feature:Athlete (nivel 4) 1", ItemModifierTests.Text(sheet.Breakdowns["ability.dex"]));
    }

    [Fact]
    public void Race_proficiency_and_spellcasting_prerequisites_are_parsed_and_normalized()
    {
        var option = new OptionDefinition
        {
            Index = "x",
            SetId = "s",
            Name = "X",
            PrerequisitesJson = """{"races":["Elf","half-elf","elf"],"proficiency":{"armor":["heavy","medium-armor","plate"],"weapon":["martial","longswords"]},"spellcasting":true}""",
        };

        var prerequisites = option.Prerequisites;

        Assert.Equal(["elf", "half-elf"], prerequisites.Races);
        Assert.Equal([ProficiencyKeys.HeavyArmor, ProficiencyKeys.MediumArmor], prerequisites.ArmorProficiencies);
        Assert.Equal([ProficiencyKeys.MartialWeapons, "longswords"], prerequisites.WeaponProficiencies);
        Assert.True(prerequisites.Spellcasting);
        Assert.False(prerequisites.IsEmpty);
        Assert.True(OptionPrerequisites.None.IsEmpty);
        Assert.True(ProficiencyKeys.HasArmor([ProficiencyKeys.AllArmor], ProficiencyKeys.HeavyArmor));
        Assert.False(ProficiencyKeys.HasArmor([ProficiencyKeys.AllArmor], ProficiencyKeys.Shields));
        Assert.False(ProficiencyKeys.HasArmor([ProficiencyKeys.LightArmor], ProficiencyKeys.HeavyArmor));
    }

    [Fact]
    public void Rolled_hit_points_replace_the_average_of_their_level()
    {
        var character = NewCharacter(Scores(con: 14), [new ClassEntry("fighter", null, 3)]);
        character.RecordChoice(2, "fighter", CharacterChoice.HitPointsKey, new ChoiceSelection { Kind = ChoiceSelection.HitPointsKind, Roll = 3 }, Now);

        var sheet = Sheet(character);

        // 10 + 2 (level 1), 3 + 2 (rolled at level 2), 6 + 2 (average at level 3).
        Assert.Equal(25, sheet.HitPointsMax);
        Assert.Equal("25 = class:Guerrero 3 (d10; tiradas 3) 19, ability:Constitución 6", ItemModifierTests.Text(sheet.Breakdowns["hitPointsMax"]));
    }

    [Fact]
    public void Option_resources_become_automatic_resources()
    {
        var character = NewCharacter(Scores(cha: 16), [new ClassEntry("warlock", null, 7)]);
        Pick(character, "eldritch-invocations", "OptionSet", "eldritch-invocations", DreadfulWord, level: 7);

        var sheet = SheetWith(character, EquippedGear.None, DreadfulWord);
        character.SyncAutoResources([.. ClassResourceRules.ForClasses(character.Classes, sheet.AbilityModifiers), .. sheet.ChoiceResources]);

        var resource = Assert.Single(character.Resources, r => r.Key == "dreadful-word");
        Assert.Equal((1, ResourceRecharge.LongRest, true), (resource.Max, resource.Recharge, resource.IsAuto));
    }

    [Fact]
    public void Archery_adds_2_to_ranged_attacks_only()
    {
        var character = NewCharacter(Scores(str: 10, dex: 16), [new ClassEntry("fighter", null, 1)], [new ProficiencyEntry(ProficiencyType.Weapon, "simple-weapons")]);
        Pick(character, "fighting-style", "OptionSet", "fighting-styles", Archery, level: 1);

        var attacks = AttacksWith(character, [Archery], Shortbow, Dagger);

        var bow = attacks.Single(a => a.Name == "Shortbow");
        Assert.Equal(3 + 2 + 2, bow.AttackBonus);
        Assert.Contains(bow.AttackBreakdown.Parts, p => p is { Source: BreakdownSources.Feature, Label: "Archery (nivel 1)", Value: 2 });
        Assert.Equal(3 + 2, attacks.Single(a => a.Name == "Dagger").AttackBonus);
        Assert.Equal(0 + 2, attacks.Single(a => a.Name == CombatCalculator.UnarmedStrikeName).AttackBonus);
    }

    [Fact]
    public void Dueling_adds_2_damage_to_a_one_handed_weapon_alone_but_not_to_its_two_handed_grip()
    {
        var character = NewCharacter(Scores(str: 16), [new ClassEntry("fighter", null, 1)], [new ProficiencyEntry(ProficiencyType.Weapon, "martial-weapons")]);
        Pick(character, "fighting-style", "OptionSet", "fighting-styles", Dueling, level: 1);

        var alone = AttacksWith(character, [Dueling], TestItems.Longsword).Single(a => a.Name == "Longsword");
        var withDagger = AttacksWith(character, [Dueling], TestItems.Longsword, Dagger).Single(a => a.Name == "Longsword");

        Assert.Equal(("1d8+5", "1d10+3"), (alone.Damage, alone.VersatileDamage));
        Assert.Contains(alone.DamageBreakdown.Parts, p => p is { Source: BreakdownSources.Feature, Label: "Dueling (nivel 1)", Value: 2 });
        Assert.Equal("1d8+3", withDagger.Damage);
    }

    [Fact]
    public void Replaced_picks_are_no_longer_active()
    {
        var character = NewCharacter(Scores(cha: 16), [new ClassEntry("warlock", null, 3)]);
        character.RecordChoice(2, "warlock", "eldritch-invocations", new ChoiceSelection
        {
            Kind = "OptionSet",
            SetId = "eldritch-invocations",
            Selected = [new ChoiceItem("a", "A"), new ChoiceItem("b", "B")],
        }, Now);
        character.RecordChoice(3, "warlock", "eldritch-invocations", new ChoiceSelection
        {
            Kind = "OptionSet",
            SetId = "eldritch-invocations",
            Selected = [new ChoiceItem("c", "C")],
            Replaced = [new ChoiceItem("a", "A")],
        }, Now);

        Assert.Equal(["b", "c"], character.ActivePicks().Select(p => p.Item.Index).Order());
    }

    [Fact]
    public void Multiclassing_checks_the_new_class_and_the_current_ones()
    {
        static Func<string, int> Scores(int str = 10, int dex = 10, int @int = 10, int cha = 10) =>
            a => a switch { "str" => str, "dex" => dex, "int" => @int, "cha" => cha, _ => 10 };

        Assert.Equal("Mago requiere Inteligencia 13.", MulticlassRules.WhyNot("wizard", ["fighter"], Scores(str: 16, @int: 12)));
        Assert.Null(MulticlassRules.WhyNot("wizard", ["fighter"], Scores(str: 16, @int: 13)));
        Assert.Null(MulticlassRules.WhyNot("fighter", ["wizard"], Scores(dex: 13, @int: 13)));
        Assert.Equal("Para salir de Mago hace falta Inteligencia 13.", MulticlassRules.WhyNot("fighter", ["wizard"], Scores(str: 13, @int: 12)));
        Assert.Equal("Paladín requiere Fuerza 13 y Carisma 13.", MulticlassRules.WhyNot("paladin", [], Scores()));
    }

    [Fact]
    public void Level_up_needs_a_grant_for_players_but_not_for_dms()
    {
        var character = Fighter(Scores());

        Assert.Throws<DomainException>(() => character.LevelUpTarget(actorIsDm: false));
        Assert.Equal(2, character.LevelUpTarget(actorIsDm: true));

        character.GrantLevelUp(Guid.NewGuid(), Now);
        Assert.Equal(2, character.LevelUpTarget(actorIsDm: false));
        character.AdvanceClass("wizard", Now);

        Assert.Null(character.PendingLevelUpTo);
        Assert.Equal([("fighter", 1, 0), ("wizard", 1, 1)], character.OrderedClasses.Select(c => (c.ClassIndex, c.Level, c.Order)));
    }

    [Fact]
    public void Seed_json_is_parsed_tolerantly()
    {
        var option = new OptionDefinition
        {
            Index = "x",
            SetId = "s",
            Name = "X",
            PrerequisitesJson = """{"minLevel":5,"pactBoon":"pact-of-the-blade","cantrip":null,"abilities":{"STR":13}}""",
            ModifiersJson = """[{"kind":"Nope","value":1},{"kind":"SpeedBonus","value":10}]""",
            GrantsJson = """{"skills":["deception"],"spells":[{"index":"hold-person","minLevel":3},"bane"]}""",
            ResourceJson = """{"key":"k","name":"K","max":"proficiencyBonus","recharge":"ShortRest"}""",
        };

        Assert.Equal((5, "pact-of-the-blade", (string?)null, 13), (option.Prerequisites.MinLevel, option.Prerequisites.PactBoon, option.Prerequisites.Cantrip, option.Prerequisites.Abilities["str"]));
        Assert.Equal([new ChoiceModifier(ItemModifierKind.SpeedBonus, null, 10, null)], option.Modifiers);
        Assert.Equal([new GrantedSpell("hold-person", 3), new GrantedSpell("bane", null)], option.Grants.Spells);
        Assert.Equal(3, option.Resource!.Evaluate(3, 5, _ => 0));
        Assert.Equal(ResourceRecharge.ShortRest, option.Resource.Recharge);
        Assert.Empty(new OptionDefinition { Index = "y", SetId = "s", Name = "Y", ModifiersJson = "not json" }.Modifiers);
    }

    private static OptionDefinition Option(string index, string name, string modifiers) =>
        new() { Index = index, SetId = OptionSets.FightingStyles, Name = name, ModifiersJson = modifiers };

    private static Dnd5eCharacter Fighter(AbilityScores scores) => NewCharacter(scores, [new ClassEntry("fighter", null, 1)]);

    private static void Pick(Dnd5eCharacter character, string key, string kind, string setId, OptionDefinition option, int level)
    {
        var classIndex = character.OrderedClasses[0].ClassIndex;
        character.RecordChoice(level, classIndex, key, new ChoiceSelection
        {
            Kind = kind,
            Name = key,
            SetId = setId,
            Selected = [new ChoiceItem(option.Index, option.Name)],
        }, Now);
    }

    private static CharacterSheet SheetWith(Dnd5eCharacter character, EquippedGear gear, params OptionDefinition[] extra)
    {
        var options = new[] { Defense, Archery, Dueling }.Concat(extra).DistinctBy(o => o.Index).ToDictionary(o => o.Index);
        var effects = ChoiceEffects.Build(character, i => options.GetValueOrDefault(i));
        return SheetCalculator.Calculate(new SheetInput(character, Classes, null, null, Skills, gear, effects));
    }

    private static IReadOnlyList<AttackValue> AttacksWith(Dnd5eCharacter character, OptionDefinition[] options, params ItemTemplate[] weapons)
    {
        var sheet = SheetWith(character, EquippedGear.None, options);
        return CombatCalculator.Attacks(character, sheet, weapons.Select(w => new EquippedWeapon(Guid.NewGuid(), w.Index, TestItems.Effective(w))));
    }
}
