using Dnd.Domain.Catalog;
using Dnd.Domain.Characters;
using Dnd.Domain.Common;
using static Dnd.Domain.Tests.Characters.TestCatalog;

namespace Dnd.Domain.Tests.Characters;

/// <summary>Phase 19: concentration checks, Natural Recovery, rolls after rests, origin choices and invalid picks.</summary>
public class ForcedDecisionsTests
{
    // ---- Concentration -------------------------------------------------------------------------------

    [Theory]
    [InlineData(30, 15)]
    [InlineData(8, 10)]
    [InlineData(21, 10)]
    [InlineData(23, 11)]
    public void Concentration_dc_is_half_the_damage_with_a_minimum_of_10(int damage, int dc)
    {
        var cleric = ActiveCleric(out _);
        cleric.SetConcentration("bless", Now);

        var result = cleric.ApplyDamage(damage, Now);

        Assert.Equal((dc, false), (result.ConcentrationCheckDc, result.ConcentrationEnded));
        Assert.Equal("bless", cleric.ConcentratingOnSpellIndex);
        Assert.Equal(dc, Character.ConcentrationCheckDc(damage));
    }

    [Fact]
    public void Damage_absorbed_by_temporary_hit_points_still_asks_for_the_save()
    {
        var cleric = ActiveCleric(out var sheet);
        cleric.ApplyCombatUpdate(new CombatUpdate { TemporaryHitPoints = 40 }, sheet.HitPointsMax, Now);
        cleric.SetConcentration("bless", Now);

        var result = cleric.ApplyDamage(30, Now);

        Assert.Equal((30, 15, sheet.HitPointsMax), (result.AbsorbedByTemporary, result.ConcentrationCheckDc, result.HitPointsCurrent));
    }

    [Fact]
    public void Dropping_to_0_hit_points_ends_the_concentration_without_a_save()
    {
        var cleric = ActiveCleric(out _);
        cleric.SetConcentration("bless", Now);

        var result = cleric.ApplyDamage(500, Now);

        Assert.Equal((0, (int?)null, true, "bless"), (result.HitPointsCurrent, result.ConcentrationCheckDc, result.ConcentrationEnded, result.ConcentratingOn));
        Assert.Null(cleric.ConcentratingOnSpellIndex);
    }

    [Fact]
    public void Without_concentration_there_is_no_check_and_setting_0_hit_points_by_hand_also_ends_it()
    {
        var cleric = ActiveCleric(out var sheet);
        Assert.Null(cleric.ApplyDamage(30, Now).ConcentrationCheckDc);

        cleric.SetConcentration("bless", Now);
        cleric.ApplyCombatUpdate(new CombatUpdate { HitPointsCurrent = 0 }, sheet.HitPointsMax, Now);

        Assert.Null(cleric.ConcentratingOnSpellIndex);
    }

    // ---- Natural Recovery ----------------------------------------------------------------------------

    [Fact]
    public void Natural_recovery_needs_a_circle_of_the_land_druid_and_no_slot_of_6th_level()
    {
        var land = WithResources(NewCharacter(Scores(wis: 16), [new ClassEntry("druid", "land", 11)]));
        var moon = WithResources(NewCharacter(Scores(wis: 16), [new ClassEntry("druid", "moon", 11)]));
        var sheet = Sheet(land);
        land.SpendSpellSlot(6, 1, sheet.SpellSlotMax(6), Now);
        land.SpendSpellSlot(3, 1, sheet.SpellSlotMax(3), Now);
        land.SpendSpellSlot(2, 1, sheet.SpellSlotMax(2), Now);
        land.SpendSpellSlot(1, 2, sheet.SpellSlotMax(1), Now);

        Assert.Contains(land.Resources, r => r.Key == ClassResourceRules.NaturalRecovery);
        Assert.DoesNotContain(moon.Resources, r => r.Key == ClassResourceRules.NaturalRecovery);
        Assert.Throws<DomainException>(() => moon.NaturalRecovery([1], Now));
        Assert.Throws<DomainException>(() => land.NaturalRecovery([6], Now)); // no slot of 6th level or higher
        Assert.Throws<DomainException>(() => land.NaturalRecovery([3, 2, 1, 1], Now)); // 7 > ceil(11 / 2) = 6
        land.NaturalRecovery([3, 2, 1], Now);

        Assert.Equal((1, 0, 0, 1), (land.SpellSlotsUsed(1), land.SpellSlotsUsed(2), land.SpellSlotsUsed(3), land.SpellSlotsUsed(6)));
        Assert.Throws<DomainException>(() => land.NaturalRecovery([1], Now));
        var apprentice = WithResources(NewCharacter(Scores(wis: 16), [new ClassEntry("druid", "land", 1)]));
        Assert.DoesNotContain(apprentice.Resources, r => r.Key == ClassResourceRules.NaturalRecovery);
    }

    // ---- Rolls after rests ---------------------------------------------------------------------------

    [Fact]
    public void A_resource_that_rolls_after_long_rests_is_pending_until_its_values_are_written()
    {
        var wizard = NewCharacter(Scores(@int: 16), [new ClassEntry("wizard", null, 2)]);
        var sheet = Sheet(wizard);
        wizard.SyncAutoResources([new ResourceTemplate("portent", "Portent", 2, ResourceRecharge.LongRest) { RollOnRest = new RollOnRest(20, 2, RestKind.Long) }]);
        var portent = wizard.Resources.Single();

        Assert.True(portent.RollsPending);
        Assert.True(wizard.RestRollsPending);
        Assert.Throws<DomainException>(() => wizard.RecordResourceRolls(portent.Id, [14], Now));
        Assert.Throws<DomainException>(() => wizard.RecordResourceRolls(portent.Id, [14, 21], Now));
        wizard.RecordResourceRolls(portent.Id, [14, 3], Now);
        Assert.Equal([14, 3], portent.Rolls);
        Assert.False(portent.RollsPending);

        wizard.ShortRest(new Dictionary<string, int>(), sheet, new FixedDice(1), Now);
        Assert.False(portent.RollsPending);

        wizard.LongRest(sheet.HitPointsMax, Now);
        Assert.Empty(portent.Rolls);
        Assert.True(portent.RollsPending);

        // Syncing the same template again keeps the values.
        wizard.RecordResourceRolls(portent.Id, [1, 20], Now);
        wizard.SyncAutoResources([new ResourceTemplate("portent", "Portent", 2, ResourceRecharge.LongRest) { RollOnRest = new RollOnRest(20, 2, RestKind.Long) }]);
        Assert.Equal([1, 20], portent.Rolls);

        var plain = NewCharacter();
        plain.SyncAutoResources([new ResourceTemplate("rage", "Rage", 2, ResourceRecharge.LongRest)]);
        Assert.Throws<DomainException>(() => plain.RecordResourceRolls(plain.Resources.Single().Id, [1], Now));
    }

    [Theory]
    [InlineData("d20", 20)]
    [InlineData("1d6", 6)]
    [InlineData("D8", 8)]
    [InlineData("d7", null)]
    [InlineData("20", null)]
    public void Roll_dice_are_parsed(string dice, int? die) => Assert.Equal(die, RollOnRest.ParseDie(dice));

    [Fact]
    public void Option_resources_read_their_roll_on_rest()
    {
        var resource = LevelChoiceJson.ParseResource("""{"key":"omen","name":"Omen","max":2,"recharge":"LongRest","rollOnRest":{"dice":"d20","count":2,"rest":"long"}}""");

        Assert.Equal(new RollOnRest(20, 2, RestKind.Long), resource!.RollOnRest);
        Assert.Null(LevelChoiceJson.ParseResource("""{"key":"omen","name":"Omen","max":2,"rollOnRest":{"dice":"d3","count":2,"rest":"long"}}""")!.RollOnRest);
    }

    // ---- Origin choices ------------------------------------------------------------------------------

    [Fact]
    public void Chosen_racial_ability_bonuses_go_to_the_race_breakdown_and_respect_apply_racial_bonuses()
    {
        var character = NewCharacter(Scores(dex: 14, con: 13, cha: 15), [new ClassEntry("rogue", null, 1)]);
        var halfElf = new RaceInfo(30, [new AbilityBonus("cha", 2)]) { Name = "Half-Elf" };
        character.RecordOriginChoice(
            "race.abilityBonuses",
            new ChoiceSelection { Kind = OriginChoiceKeys.AbilityBonusKind, Asi = new Dictionary<string, int> { ["dex"] = 1, ["con"] = 1 } },
            Now);

        var sheet = SheetWithChoices(character, halfElf);

        Assert.Equal((15, 14, 17), (sheet.Abilities["dex"].Score, sheet.Abilities["con"].Score, sheet.Abilities["cha"].Score));
        Assert.Contains(sheet.Breakdowns["ability.dex"].Parts, p => p is { Source: BreakdownSources.Race, Label: "Raza (elección)", Value: 1 });
        Assert.DoesNotContain(character.Choices, c => !c.IsOrigin);

        character.ApplySheetEdit(new SheetEdit { ApplyRacialBonuses = false }, Now);
        Assert.Equal(14, SheetWithChoices(character, halfElf).Abilities["dex"].Score);
    }

    [Fact]
    public void Origin_skills_become_race_proficiencies_survive_full_edits_and_go_away_with_the_race()
    {
        var character = NewCharacter(classes: [new ClassEntry("rogue", null, 1)]);
        character.ApplySheetEdit(new SheetEdit { RaceIndex = "half-elf" }, Now);
        character.RecordOriginChoice(
            "race.skills",
            new ChoiceSelection { Kind = OriginChoiceKeys.SkillKind, Selected = [new ChoiceItem("stealth", "Stealth"), new ChoiceItem("perception", "Perception")] },
            Now);

        Assert.Equal(ProficiencySource.Race, character.FindProficiency(ProficiencyType.Skill, "stealth")!.Source);

        character.ApplySheetEdit(new SheetEdit { Proficiencies = [new ProficiencyEntry(ProficiencyType.Skill, "acrobatics")] }, Now);
        Assert.NotNull(character.FindProficiency(ProficiencyType.Skill, "perception"));

        // Answering again replaces the earlier picks.
        character.RecordOriginChoice(
            "race.skills",
            new ChoiceSelection { Kind = OriginChoiceKeys.SkillKind, Selected = [new ChoiceItem("stealth", "Stealth"), new ChoiceItem("athletics", "Athletics")] },
            Now);
        Assert.Null(character.FindProficiency(ProficiencyType.Skill, "perception"));
        Assert.Single(character.OriginChoices);

        character.ApplySheetEdit(new SheetEdit { RaceIndex = "human" }, Now);
        Assert.Empty(character.OriginChoices);
        Assert.Null(character.FindProficiency(ProficiencyType.Skill, "stealth"));
        Assert.NotNull(character.FindProficiency(ProficiencyType.Skill, "acrobatics"));
        Assert.Throws<DomainException>(() => character.RecordOriginChoice("skills", new ChoiceSelection(), Now));
    }

    [Fact]
    public void A_dragonborn_ancestry_gives_its_resistance_and_breath_weapon()
    {
        var character = NewCharacter(Scores(con: 14), [new ClassEntry("fighter", null, 6)]);
        var red = new TraitOption("draconic-ancestry-red", "Draconic Ancestry (Red)", [])
        {
            DamageType = "fire",
            BreathWeapon = new BreathWeaponInfo("Breath Weapon", "15 ft. cone", "dex", new Dictionary<int, string> { [1] = "2d6", [6] = "3d6", [11] = "4d6" }),
        };
        var dragonborn = new RaceInfo(30, [new AbilityBonus("str", 2)])
        {
            Name = "Dragonborn",
            Choices = new RaceChoices { TraitOptions = [new TraitOptionChoice("draconic-ancestry", "Draconic Ancestry", 1, [red])] },
        };
        Assert.Empty(SheetWithChoices(character, dragonborn).Resistances);

        character.RecordOriginChoice(
            "race.trait.draconic-ancestry",
            new ChoiceSelection { Kind = OriginChoiceKeys.TraitOptionKind, Selected = [new ChoiceItem(red.Index, red.Name)] },
            Now);
        var sheet = SheetWithChoices(character, dragonborn);

        Assert.Equal(new ResistanceValue("fire", BreakdownSources.Race, "Draconic Ancestry (Red)"), Assert.Single(sheet.Resistances));
        Assert.Equal(new BreathWeaponValue("Breath Weapon", BreakdownSources.Race, "fire", "3d6", "dex", "15 ft. cone", 8 + 2 + 3), sheet.BreathWeapon);
        Assert.Equal(sheet.BreathWeapon!.Dc, sheet.Breakdowns[SheetCalculator.BreathWeaponDcKey].Total);
    }

    [Fact]
    public void Race_resistances_are_listed_with_their_race()
    {
        var sheet = Sheet(NewCharacter(), Dwarf with { Name = "Dwarf", Resistances = ["poison"] });

        Assert.Equal(new ResistanceValue("poison", BreakdownSources.Race, "Dwarf"), Assert.Single(sheet.Resistances));
    }

    [Fact]
    public void Race_choices_round_trip_through_json()
    {
        var choices = new RaceChoices
        {
            AbilityBonuses = new AbilityBonusChoice(2, 1, [new OriginOption("str", "STR"), new OriginOption("dex", "DEX")]),
            Languages = new PickChoice(1, []),
            Cantrip = new CantripChoice(1, "wizard", [new OriginOption("light", "Light")]),
            TraitOptions = [new TraitOptionChoice("ancestry", "Ancestry", 1, [new TraitOption("a", "A", ["x"]) { DamageType = "cold" }])],
        };

        var parsed = RaceChoices.Parse(choices.ToJson());

        Assert.Equal((2, 1, "dex"), (parsed.AbilityBonuses!.Choose, parsed.AbilityBonuses.Amount, parsed.AbilityBonuses.From[1].Index));
        Assert.Equal(("wizard", "cold"), (parsed.Cantrip!.SpellList, parsed.TraitOptions[0].Options[0].DamageType));
        Assert.Empty(parsed.Languages!.From);
        Assert.Null(RaceChoices.None.ToJson());
        Assert.True(RaceChoices.Parse("not json").IsEmpty);
    }

    // ---- Multiclassing skills and invalid picks ------------------------------------------------------

    [Theory]
    [InlineData("bard", 1, false)]
    [InlineData("ranger", 1, true)]
    [InlineData("rogue", 1, true)]
    public void Bards_rangers_and_rogues_give_a_skill_when_multiclassing(string classIndex, int choose, bool fromList) =>
        Assert.Equal((choose, fromList), MulticlassRules.SkillsFor(classIndex));

    [Fact]
    public void Other_classes_give_no_skill_when_multiclassing() => Assert.Null(MulticlassRules.SkillsFor("fighter"));

    [Fact]
    public void A_feat_whose_ability_prerequisite_is_lost_is_invalid()
    {
        var grappler = new OptionDefinition
        {
            Index = "grappler",
            SetId = OptionSets.Feats,
            Name = "Grappler",
            PrerequisitesJson = """{"abilities":{"str":13}}""",
        };
        var character = NewCharacter(Scores(str: 13), [new ClassEntry("fighter", null, 4)]);
        character.RecordChoice(4, "fighter", "asi", new ChoiceSelection { Kind = nameof(LevelChoiceKind.AsiOrFeat), SetId = OptionSets.Feats, Feat = new ChoiceItem("grappler", "Grappler") }, Now);
        OptionDefinition? Find(string index) => index == "grappler" ? grappler : null;

        Assert.Empty(ChoiceValidity.Find(character, Sheet(character), Find));

        character.SetBaseAbilities(Scores(str: 10), Now);
        var invalid = Assert.Single(ChoiceValidity.Find(character, Sheet(character), Find));

        Assert.Equal(("fighter", "asi", OptionSets.Feats, "grappler"), (invalid.ClassIndex, invalid.Key, invalid.SetId, invalid.Item.Index));
        Assert.Contains("Fuerza 13", invalid.Reason);

        // Replacing it later removes it from the picks and from the sheet effects.
        character.RecordChoice(4, "fighter", "asi", new ChoiceSelection { Kind = nameof(LevelChoiceKind.AsiOrFeat), SetId = OptionSets.Feats, Replaced = [invalid.Item] }, Now.AddMinutes(1));
        Assert.Empty(ChoiceValidity.Find(character, Sheet(character), Find));
        Assert.DoesNotContain(character.ActivePicks(), p => p.Item.Index == "grappler");
    }

    [Fact]
    public void An_invocation_needs_its_pact_boon_and_its_level()
    {
        var thirsting = new OptionDefinition
        {
            Index = "thirsting-blade",
            SetId = "eldritch-invocations",
            Name = "Thirsting Blade",
            PrerequisitesJson = """{"minLevel":5,"pactBoon":"pact-of-the-blade"}""",
        };
        var warlock = NewCharacter(Scores(cha: 16), [new ClassEntry("warlock", null, 5)]);
        warlock.RecordChoice(5, "warlock", "eldritch-invocations", new ChoiceSelection { Kind = nameof(LevelChoiceKind.OptionSet), SetId = "eldritch-invocations", Selected = [new ChoiceItem("thirsting-blade", "Thirsting Blade")] }, Now);

        var invalid = Assert.Single(ChoiceValidity.Find(warlock, Sheet(warlock), i => i == thirsting.Index ? thirsting : null));

        Assert.Contains("pact-of-the-blade", invalid.Reason);
        Assert.DoesNotContain("nivel", invalid.Reason);
    }

    // ---- Helpers ---------------------------------------------------------------------------------------

    private static Character ActiveCleric(out CharacterSheet sheet)
    {
        var character = NewCharacter(Scores(con: 16, wis: 16), [new ClassEntry("cleric", null, 10)]);
        sheet = Sheet(character);
        character.Activate(sheet.HitPointsMax, Now);
        return character;
    }

    private static Character WithResources(Character character)
    {
        character.SyncAutoResources(ClassResourceRules.ForClasses(character.Classes, Sheet(character).AbilityModifiers));
        return character;
    }

    private static CharacterSheet SheetWithChoices(Character character, RaceInfo race) =>
        SheetCalculator.Calculate(new SheetInput(character, Classes, race, null, Skills, null, ChoiceEffects.Build(character, _ => null)));
}
