using Dnd.Domain.Catalog;
using Dnd.Domain.Characters;
using static Dnd.Domain.Tests.Characters.TestCatalog;

namespace Dnd.Domain.Tests.Characters;

public class SheetCalculatorTests
{
    [Fact]
    public void Barbarian_1_with_Str_16_Con_14_has_14_hp_and_unarmored_defense()
    {
        var character = NewCharacter(Scores(str: 16, dex: 14, con: 14), [new ClassEntry("barbarian", null, 1)]);

        var sheet = Sheet(character);

        Assert.Equal(14, sheet.HitPointsMax);
        Assert.Equal(10 + 2 + 2, sheet.ArmorClass);
        Assert.Equal(1, sheet.TotalLevel);
        Assert.Equal(2, sheet.ProficiencyBonus);
        Assert.Equal(new AbilityValue(16, 3, false), sheet.Abilities["str"]);
        Assert.Equal(new HitDiceValue("barbarian", 12, 1, 1), Assert.Single(sheet.HitDice));
        Assert.Empty(sheet.Spellcasting);
        Assert.All(sheet.SpellSlotsMax, s => Assert.Equal(0, s));
    }

    [Fact]
    public void Wizard_5_with_Int_18_has_dc_15_attack_7_and_full_caster_slots()
    {
        var character = NewCharacter(Scores(@int: 18), [new ClassEntry("wizard", null, 5)]);

        var sheet = Sheet(character);

        var casting = Assert.Single(sheet.Spellcasting);
        Assert.Equal(new SpellcastingValue("wizard", "int", 15, 7, 9) { MaxSpellLevel = 3 }, casting);
        Assert.Equal([4, 3, 2, 0, 0, 0, 0, 0, 0], sheet.SpellSlotsMax);
        Assert.Equal(3, sheet.SpellSlotMax(2));
        Assert.Null(sheet.PactMagic);
        Assert.Equal(0, sheet.SpellSlotMax(SpellSlotState.PactLevel));
    }

    [Fact]
    public void Paladin_4_wizard_2_uses_the_multiclass_table_at_caster_level_4()
    {
        var character = NewCharacter(Scores(@int: 14, cha: 16), [new ClassEntry("paladin", null, 4), new ClassEntry("wizard", null, 2)]);

        var sheet = Sheet(character);

        Assert.Equal(4, sheet.MulticlassCasterLevel);
        Assert.Equal([4, 3, 0, 0, 0, 0, 0, 0, 0], sheet.SpellSlotsMax);
        Assert.Equal(["paladin", "wizard"], sheet.Spellcasting.Select(s => s.ClassIndex));
        Assert.Equal(3 + 4 / 2, sheet.Spellcasting[0].PreparedMax);
        Assert.Equal(2 + 2, sheet.Spellcasting[1].PreparedMax);
    }

    [Fact]
    public void Single_half_caster_uses_its_own_table()
    {
        var character = NewCharacter(Scores(cha: 14), [new ClassEntry("paladin", null, 5)]);

        Assert.Equal([4, 2, 0, 0, 0, 0, 0, 0, 0], Sheet(character).SpellSlotsMax);
    }

    [Fact]
    public void Warlock_3_has_two_pact_slots_of_level_2()
    {
        var character = NewCharacter(Scores(cha: 16), [new ClassEntry("warlock", null, 3)]);

        var sheet = Sheet(character);

        Assert.Equal(new PactMagicValue(2, 2), sheet.PactMagic);
        Assert.Equal(2, sheet.SpellSlotMax(SpellSlotState.PactLevel));
        Assert.All(sheet.SpellSlotsMax, s => Assert.Equal(0, s));
        Assert.Equal(new SpellcastingValue("warlock", "cha", 13, 5, null) { MaxSpellLevel = 2 }, Assert.Single(sheet.Spellcasting));
    }

    [Fact]
    public void Warlock_levels_do_not_merge_into_the_multiclass_table()
    {
        var character = NewCharacter(classes: [new ClassEntry("wizard", null, 3), new ClassEntry("warlock", null, 2)]);

        var sheet = Sheet(character);

        Assert.Equal([4, 2, 0, 0, 0, 0, 0, 0, 0], sheet.SpellSlotsMax);
        Assert.Equal(new PactMagicValue(1, 2), sheet.PactMagic);
    }

    [Fact]
    public void Hit_points_max_override_replaces_the_calculated_value()
    {
        var character = NewCharacter(
            Scores(con: 14),
            [new ClassEntry("fighter", null, 3)],
            overrides: [new OverrideEntry(OverrideFields.HitPointsMax, 40, "Tiradas reales")]);

        var sheet = Sheet(character);

        Assert.Equal(40, sheet.HitPointsMax);
        Assert.Equal([OverrideFields.HitPointsMax], sheet.OverriddenFields);
        Assert.True(sheet.IsOverridden(OverrideFields.HitPointsMax));
    }

    [Fact]
    public void Manual_hp_mode_takes_the_override()
    {
        var character = NewCharacter(classes: [new ClassEntry("fighter", null, 3)], overrides: [new OverrideEntry(OverrideFields.HitPointsMax, 31)]);
        character.SetHpMode(HpMode.Manual, Now);

        Assert.Equal(31, Sheet(character).HitPointsMax);
    }

    [Fact]
    public void Average_hit_points_add_half_die_plus_one_per_extra_level_across_classes()
    {
        var character = NewCharacter(Scores(con: 14), [new ClassEntry("fighter", null, 2), new ClassEntry("wizard", null, 1)]);

        // fighter 1: 10 + 2; fighter 2: 6 + 2; wizard 1: 4 + 2
        Assert.Equal(26, Sheet(character).HitPointsMax);
    }

    [Fact]
    public void Each_level_gives_at_least_one_hit_point()
    {
        var character = NewCharacter(Scores(con: 1), [new ClassEntry("wizard", null, 3)]);

        Assert.Equal(3, Sheet(character).HitPointsMax);
    }

    [Fact]
    public void Rogue_1_with_stealth_expertise_and_Dex_16_has_plus_7()
    {
        var character = NewCharacter(
            Scores(dex: 16),
            [new ClassEntry("rogue", null, 1)],
            [new ProficiencyEntry(ProficiencyType.Skill, "stealth", Expertise: true, ProficiencySource.Class)]);

        var sheet = Sheet(character);

        var stealth = sheet.Skills.Single(s => s.Index == "stealth");
        Assert.Equal(new SkillValue("stealth", "Stealth", "dex", 7, true, true, false), stealth);
        Assert.Equal(3, sheet.Skills.Single(s => s.Index == "acrobatics").Value);
        Assert.Equal(3, sheet.Initiative);
    }

    [Fact]
    public void Dataset_style_proficiency_keys_are_recognised()
    {
        var character = NewCharacter(
            Scores(str: 14),
            [new ClassEntry("fighter", null, 1)],
            [new ProficiencyEntry(ProficiencyType.Skill, "skill-athletics"), new ProficiencyEntry(ProficiencyType.SavingThrow, "saving-throw-str")]);

        var sheet = Sheet(character);

        Assert.Equal(4, sheet.Skills.Single(s => s.Index == "athletics").Value);
        Assert.Equal(new SavingThrowValue(4, true, false), sheet.SavingThrows["str"]);
    }

    [Fact]
    public void Passive_perception_is_10_plus_perception()
    {
        var character = NewCharacter(Scores(wis: 14), [new ClassEntry("cleric", null, 1)], [new ProficiencyEntry(ProficiencyType.Skill, "perception")]);

        Assert.Equal(14, Sheet(character).PassivePerception);
    }

    [Fact]
    public void Passive_perception_follows_overrides()
    {
        var withSkillOverride = NewCharacter(Scores(wis: 14), [new ClassEntry("cleric", null, 1)], overrides: [new OverrideEntry(OverrideFields.Skill("perception"), 6)]);
        var withPassiveOverride = NewCharacter(Scores(wis: 14), [new ClassEntry("cleric", null, 1)], overrides: [new OverrideEntry(OverrideFields.PassivePerception, 20)]);

        Assert.Equal(16, Sheet(withSkillOverride).PassivePerception);
        Assert.True(Sheet(withSkillOverride).Skills.Single(s => s.Index == "perception").Overridden);
        Assert.Equal(20, Sheet(withPassiveOverride).PassivePerception);
    }

    [Fact]
    public void Saving_throws_add_proficiency_only_when_proficient()
    {
        var character = NewCharacter(
            Scores(str: 16, con: 14),
            [new ClassEntry("barbarian", null, 5)],
            [new ProficiencyEntry(ProficiencyType.SavingThrow, "str", Source: ProficiencySource.Class), new ProficiencyEntry(ProficiencyType.SavingThrow, "con", Source: ProficiencySource.Class)]);

        var sheet = Sheet(character);

        Assert.Equal(new SavingThrowValue(6, true, false), sheet.SavingThrows["str"]);
        Assert.Equal(new SavingThrowValue(5, true, false), sheet.SavingThrows["con"]);
        Assert.Equal(new SavingThrowValue(0, false, false), sheet.SavingThrows["dex"]);
        Assert.Equal(Abilities.All, sheet.SavingThrows.Keys);
    }

    [Fact]
    public void Chain_mail_without_dex_plus_shield_is_18()
    {
        var character = NewCharacter(Scores(dex: 14), [new ClassEntry("fighter", null, 1)]);

        var sheet = Sheet(character, gear: new EquippedGear(16, AddDexModifier: false, MaxDexBonus: null, HasShield: true));

        Assert.Equal(18, sheet.ArmorClass);
    }

    [Fact]
    public void Medium_armor_caps_the_dex_bonus()
    {
        var character = NewCharacter(Scores(dex: 16), [new ClassEntry("fighter", null, 1)]);

        Assert.Equal(16, Sheet(character, gear: new EquippedGear(14, true, 2, false)).ArmorClass);
        Assert.Equal(15, Sheet(character, gear: new EquippedGear(12, true, null, false)).ArmorClass);
    }

    [Fact]
    public void Barbarian_unarmored_defense_does_not_apply_with_armor()
    {
        var character = NewCharacter(Scores(dex: 14, con: 16), [new ClassEntry("barbarian", null, 1)]);

        Assert.Equal(15, Sheet(character).ArmorClass);
        Assert.Equal(17, Sheet(character, gear: EquippedGear.None with { HasShield = true }).ArmorClass);
        Assert.Equal(13, Sheet(character, gear: new EquippedGear(11, true, null, false)).ArmorClass);
    }

    [Fact]
    public void Monk_unarmored_defense_needs_no_shield()
    {
        var character = NewCharacter(Scores(dex: 16, wis: 14), [new ClassEntry("monk", null, 1)]);

        Assert.Equal(15, Sheet(character).ArmorClass);
        Assert.Equal(15, Sheet(character, gear: EquippedGear.None with { HasShield = true }).ArmorClass);
    }

    [Fact]
    public void Armor_class_override_replaces_everything()
    {
        var character = NewCharacter(classes: [new ClassEntry("fighter", null, 1)], overrides: [new OverrideEntry(OverrideFields.ArmorClass, 21)]);

        Assert.Equal(21, Sheet(character, gear: new EquippedGear(16, false, null, true)).ArmorClass);
    }

    [Fact]
    public void Racial_bonuses_add_race_and_subrace_unless_disabled()
    {
        var character = NewCharacter(Scores(con: 14, wis: 12), [new ClassEntry("fighter", null, 1)]);

        var sheet = Sheet(character, Dwarf, HillDwarf);
        Assert.Equal(new AbilityValue(16, 3, false), sheet.Abilities["con"]);
        Assert.Equal(new AbilityValue(13, 1, false), sheet.Abilities["wis"]);
        Assert.Equal(25, sheet.Speed);
        Assert.Equal(13, sheet.HitPointsMax);

        character.ApplySheetEdit(new SheetEdit { ApplyRacialBonuses = false }, Now);
        Assert.Equal(14, Sheet(character, Dwarf, HillDwarf).Abilities["con"].Score);
    }

    [Fact]
    public void Ability_override_replaces_the_final_score_and_feeds_derived_values()
    {
        var character = NewCharacter(Scores(dex: 10), [new ClassEntry("fighter", null, 1)], overrides: [new OverrideEntry(OverrideFields.Ability("dex"), 18)]);

        var sheet = Sheet(character, Dwarf);

        Assert.Equal(new AbilityValue(18, 4, true), sheet.Abilities["dex"]);
        Assert.Equal(4, sheet.Initiative);
        Assert.Equal(14, sheet.ArmorClass);
    }

    [Fact]
    public void Proficiency_bonus_override_feeds_skills_and_spellcasting()
    {
        var character = NewCharacter(
            Scores(@int: 16),
            [new ClassEntry("wizard", null, 1)],
            [new ProficiencyEntry(ProficiencyType.Skill, "stealth")],
            [new OverrideEntry(OverrideFields.ProficiencyBonus, 4), new OverrideEntry(OverrideFields.SpellSaveDc, 20)]);

        var sheet = Sheet(character);

        Assert.Equal(4, sheet.ProficiencyBonus);
        Assert.Equal(4, sheet.Skills.Single(s => s.Index == "stealth").Value);
        Assert.Equal(20, sheet.Spellcasting[0].SaveDc);
        Assert.Equal(7, sheet.Spellcasting[0].AttackBonus);
        Assert.Equal([OverrideFields.ProficiencyBonus, OverrideFields.SpellSaveDc], sheet.OverriddenFields);
    }

    [Fact]
    public void Speed_defaults_to_30_without_race_and_can_be_overridden()
    {
        Assert.Equal(30, Sheet(NewCharacter()).Speed);
        Assert.Equal(35, Sheet(NewCharacter(overrides: [new OverrideEntry(OverrideFields.Speed, 35)]), Dwarf).Speed);
    }

    [Fact]
    public void Character_without_classes_has_level_0()
    {
        var sheet = Sheet(NewCharacter());

        Assert.Equal(0, sheet.TotalLevel);
        Assert.Equal(2, sheet.ProficiencyBonus);
        Assert.Equal(0, sheet.HitPointsMax);
        Assert.Empty(sheet.HitDice);
        Assert.Equal(10, sheet.ArmorClass);
    }

    [Fact]
    public void Hit_dice_remaining_subtract_the_spent_ones()
    {
        var character = NewCharacter(Scores(con: 12), [new ClassEntry("fighter", null, 3)]);
        character.ShortRest(new Dictionary<string, int> { ["fighter"] = 2 }, Sheet(character), new FixedDice(1), Now);

        Assert.Equal(new HitDiceValue("fighter", 10, 3, 1), Sheet(character).HitDice.Single());
    }

    [Fact]
    public void Missing_class_info_is_a_programming_error()
    {
        var character = NewCharacter(classes: [new ClassEntry("sorcerer", null, 1)]);

        Assert.Throws<ArgumentException>(() => Sheet(character));
    }

    [Fact]
    public void Class_info_is_built_from_the_catalog()
    {
        var definition = new ClassDefinition { Index = "wizard", Name = "Wizard", HitDie = 6, SpellcastingAbility = "int", IsSpellcaster = true, SpellcastingLevel = 1 };
        var levels = new[]
        {
            new ClassLevel { Index = "wizard-1", ClassIndex = "wizard", Level = 1, SpellSlots = [2, 0, 0, 0, 0, 0, 0, 0, 0] },
            new ClassLevel { Index = "cleric-1", ClassIndex = "cleric", Level = 1, SpellSlots = [9, 9, 9, 9, 9, 9, 9, 9, 9] },
        };

        var info = ClassInfo.From(definition, levels);

        Assert.Equal(2, info.SlotsByLevel(1)[0]);
        Assert.Equal(0, info.SlotsByLevel(2)[0]);
        Assert.Equal(6, info.HitDie);
    }

    [Fact]
    public void Race_info_parses_the_catalog_bonuses()
    {
        var race = new RaceDefinition { Index = "dwarf", Name = "Dwarf", Speed = 25, AbilityBonusesJson = """[{"ability":"con","bonus":2}]""" };

        var info = RaceInfo.From(race);

        Assert.Equal(25, info.Speed);
        Assert.Equal(new AbilityBonus("con", 2), Assert.Single(info.AbilityBonuses));
    }

    [Fact]
    public void Multiclass_table_matches_the_srd()
    {
        Assert.Equal([2, 0, 0, 0, 0, 0, 0, 0, 0], SpellSlotTables.MulticlassSlots(1));
        Assert.Equal([4, 3, 3, 3, 2, 1, 1, 1, 1], SpellSlotTables.MulticlassSlots(17));
        Assert.Equal([4, 3, 3, 3, 3, 2, 2, 1, 1], SpellSlotTables.MulticlassSlots(20));
    }
}
