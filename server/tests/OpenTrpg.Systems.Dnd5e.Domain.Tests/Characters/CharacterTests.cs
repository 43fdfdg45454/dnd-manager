using OpenTrpg.Core.Domain.Characters;
using OpenTrpg.Core.Domain.Common;
using static OpenTrpg.Systems.Dnd5e.Domain.Tests.Characters.TestCatalog;
using OpenTrpg.Systems.Dnd5e.Domain.Characters;

namespace OpenTrpg.Systems.Dnd5e.Domain.Tests.Characters;

public class CharacterTests
{
    private readonly Guid _owner = Guid.NewGuid();
    private readonly Guid _other = Guid.NewGuid();

    [Fact]
    public void Create_starts_a_draft_with_default_scores()
    {
        var campaignId = Guid.NewGuid();

        var character = Dnd5eCharacter.Create(Character.Create(campaignId, _owner, "  Thorin  ", Now));

        Assert.Equal((campaignId, (Guid?)_owner, "Thorin", CharacterStatus.Draft, HpMode.Average, true), (character.CampaignId, character.OwnerUserId, character.Name, character.Status, character.HpMode, character.ApplyRacialBonuses));
        Assert.Equal(AbilityScores.Default, character.BaseAbilities);
        Assert.Equal(0, character.TotalLevel);
        Assert.Empty(character.Conditions);
        Assert.Empty(character.HitDiceUsed);
        Assert.Equal(Now, character.Character.UpdatedAt);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Create_rejects_empty_names(string name) =>
        Assert.Throws<DomainException>(() => Character.Create(Guid.NewGuid(), null, name, Now));

    [Fact]
    public void ApplySheetEdit_changes_only_the_given_fields()
    {
        var character = NewCharacter(ownerUserId: _owner);
        character.ApplySheetEdit(new SheetEdit { RaceIndex = "dwarf", SubraceIndex = "hill-dwarf", Notes = "n", CopperPieces = 150 }, Now);

        var later = Now.AddHours(1);
        character.ApplySheetEdit(new SheetEdit { Alignment = "Lawful Good", BaseAbilities = Scores(str: 15) }, later);

        Assert.Equal(("dwarf", "hill-dwarf", "Lawful Good", "n", 150), (character.RaceIndex, character.SubraceIndex, character.Alignment, character.Character.Notes, character.Character.Money));
        Assert.Equal(15, character.BaseStr);
        Assert.Equal(later, character.Character.UpdatedAt);
    }

    [Fact]
    public void Changing_the_race_clears_the_subrace_and_empty_string_clears_values()
    {
        var character = NewCharacter();
        character.ApplySheetEdit(new SheetEdit { RaceIndex = "dwarf", SubraceIndex = "hill-dwarf", BackgroundIndex = "acolyte" }, Now);

        character.ApplySheetEdit(new SheetEdit { RaceIndex = "elf", BackgroundIndex = "" }, Now);

        Assert.Equal("elf", character.RaceIndex);
        Assert.Null(character.SubraceIndex);
        Assert.Null(character.BackgroundIndex);
        Assert.Throws<DomainException>(() => character.ApplySheetEdit(new SheetEdit { RaceIndex = "", SubraceIndex = "high-elf" }, Now));
    }

    [Fact]
    public void Invalid_sheet_edits_change_nothing()
    {
        var character = NewCharacter(classes: [new ClassEntry("fighter", null, 1)]);

        Assert.Throws<DomainException>(() => character.ApplySheetEdit(
            new SheetEdit { Name = "Nuevo", Classes = [new ClassEntry("fighter", null, 21)] },
            Now));

        Assert.Equal("Test", character.Name);
        Assert.Equal(1, character.TotalLevel);
    }

    [Theory]
    [MemberData(nameof(InvalidClassLists))]
    public void ReplaceClasses_validates_levels_and_duplicates(ClassEntry[] classes)
    {
        var character = NewCharacter();

        var error = Assert.Throws<DomainException>(() => character.ReplaceClasses(classes, Now));
        Assert.Equal(DomainErrorKind.RuleViolation, error.Kind);
    }

    public static TheoryData<ClassEntry[]> InvalidClassLists() => new()
    {
        new[] { new ClassEntry("fighter", null, 0) },
        new[] { new ClassEntry("fighter", null, 12), new ClassEntry("wizard", null, 9) },
        new[] { new ClassEntry("fighter", null, 1), new ClassEntry("fighter", null, 2) },
        new[] { new ClassEntry(" ", null, 1) },
    };

    [Fact]
    public void ReplaceClasses_keeps_ids_sets_order_and_trims_spent_hit_dice()
    {
        var character = NewCharacter(classes: [new ClassEntry("fighter", null, 4), new ClassEntry("wizard", null, 2)]);
        var fighter = character.Classes.Single(c => c.ClassIndex == "fighter");
        character.ShortRest(new Dictionary<string, int> { ["fighter"] = 4, ["wizard"] = 1 }, Sheet(character), new FixedDice(1), Now);

        character.ReplaceClasses([new ClassEntry("rogue", null, 1), new ClassEntry("fighter", "champion", 2)], Now);

        Assert.Equal(["rogue", "fighter"], character.OrderedClasses.Select(c => c.ClassIndex));
        Assert.Same(fighter, character.Classes.Single(c => c.ClassIndex == "fighter"));
        Assert.Equal(("champion", 2, 1), (fighter.SubclassIndex, fighter.Level, fighter.Order));
        Assert.Equal(new Dictionary<string, int> { ["fighter"] = 2 }, character.HitDiceUsed);
    }

    [Fact]
    public void Proficiencies_reject_duplicates_and_expertise_outside_skills_and_tools()
    {
        var character = NewCharacter();

        Assert.Throws<DomainException>(() => character.ReplaceProficiencies([new ProficiencyEntry(ProficiencyType.Armor, "Light Armor", Expertise: true)], Now));
        Assert.Throws<DomainException>(() => character.ReplaceProficiencies([new ProficiencyEntry(ProficiencyType.Skill, "stealth"), new ProficiencyEntry(ProficiencyType.Skill, "stealth", true)], Now));

        character.ReplaceProficiencies([new ProficiencyEntry(ProficiencyType.Tool, "Thieves' Tools", true), new ProficiencyEntry(ProficiencyType.Language, "Common")], Now);
        Assert.Equal(2, character.Proficiencies.Count);
    }

    [Fact]
    public void Spells_are_unique_per_class_and_always_prepared_implies_prepared()
    {
        var character = NewCharacter();

        character.ReplaceSpells([new SpellEntry("bless", "cleric", false, AlwaysPrepared: true), new SpellEntry("bless", "paladin", false)], Now);

        Assert.True(character.Spells.Single(s => s.ClassIndex == "cleric").IsPrepared);
        Assert.Throws<DomainException>(() => character.ReplaceSpells([new SpellEntry("bless", "cleric", true), new SpellEntry("bless", "cleric", false)], Now));
    }

    [Theory]
    [InlineData("unknown", 1)]
    [InlineData("ability.luck", 10)]
    [InlineData("ability.str", 31)]
    [InlineData("ability.str", 0)]
    [InlineData("save.xyz", 1)]
    [InlineData("skill.Stealth", 1)]
    [InlineData("armorClass", -1)]
    public void Overrides_reject_unknown_fields_and_out_of_range_values(string field, int value)
    {
        var character = NewCharacter();

        Assert.Throws<DomainException>(() => character.ReplaceOverrides([new OverrideEntry(field, value)], Now));
    }

    [Fact]
    public void Override_fields_admit_the_contract_list()
    {
        Assert.All(OverrideFields.Fixed, f => Assert.True(OverrideFields.IsValid(f)));
        Assert.True(OverrideFields.IsValid("ability.cha"));
        Assert.True(OverrideFields.IsValid("save.wis"));
        Assert.True(OverrideFields.IsValid("skill.animal-handling"));
        Assert.False(OverrideFields.IsValid("skill."));
    }

    [Fact]
    public void Overrides_are_updated_in_place_by_field()
    {
        var character = NewCharacter(overrides: [new OverrideEntry(OverrideFields.Speed, 35), new OverrideEntry(OverrideFields.ArmorClass, 15)]);
        var speed = character.Overrides.Single(o => o.Field == OverrideFields.Speed);

        character.ReplaceOverrides([new OverrideEntry(OverrideFields.Speed, 40, "  Botas  ")], Now);

        Assert.Same(speed, Assert.Single(character.Overrides));
        Assert.Equal((40, "Botas"), (speed.Value, speed.Note));
    }

    [Fact]
    public void Manual_hp_mode_requires_the_hit_points_max_override()
    {
        var character = NewCharacter();

        Assert.Throws<DomainException>(() => character.SetHpMode(HpMode.Manual, Now));

        character.ApplySheetEdit(new SheetEdit { HpMode = HpMode.Manual, Overrides = [new OverrideEntry(OverrideFields.HitPointsMax, 20)] }, Now);
        Assert.Equal(HpMode.Manual, character.HpMode);
        Assert.Throws<DomainException>(() => character.ReplaceOverrides([], Now));
    }

    [Fact]
    public void Base_abilities_must_be_between_1_and_30()
    {
        var character = NewCharacter();

        Assert.Throws<DomainException>(() => character.SetBaseAbilities(Scores(str: 0), Now));
        Assert.Throws<DomainException>(() => character.SetBaseAbilities(Scores(cha: 31), Now));
    }

    [Fact]
    public void Activate_moves_a_draft_to_active_at_full_hp_once()
    {
        var character = NewCharacter();

        character.Activate(12, Now);

        Assert.Equal((CharacterStatus.Active, 12), (character.Status, character.HitPointsCurrent));
        Assert.Equal(DomainErrorKind.Conflict, Assert.Throws<DomainException>(() => character.Activate(12, Now)).Kind);
    }

    [Fact]
    public void RefreshHitPoints_fills_drafts_and_caps_active_characters()
    {
        var character = NewCharacter();
        character.RefreshHitPoints(10);
        Assert.Equal(10, character.HitPointsCurrent);

        character.Activate(10, Now);
        character.ApplyCombatUpdate(new CombatUpdate { HitPointsCurrent = 7 }, 10, Now);
        character.RefreshHitPoints(20);
        Assert.Equal(7, character.HitPointsCurrent);
        character.RefreshHitPoints(5);
        Assert.Equal(5, character.HitPointsCurrent);
    }

    [Fact]
    public void Sheet_edit_mode_depends_on_role_and_status()
    {
        var character = NewCharacter(ownerUserId: _owner);

        Assert.Equal(SheetEditMode.Direct, character.ResolveSheetEdit(_owner, actorIsDm: false));
        Assert.Equal(SheetEditMode.Direct, character.ResolveSheetEdit(_other, actorIsDm: true));
        Assert.Equal(DomainErrorKind.Forbidden, Assert.Throws<DomainException>(() => character.ResolveSheetEdit(_other, false)).Kind);

        character.Activate(10, Now);

        Assert.Equal(SheetEditMode.RequiresApproval, character.ResolveSheetEdit(_owner, false));
        Assert.Equal(SheetEditMode.Direct, character.ResolveSheetEdit(_other, true));
    }

    [Fact]
    public void Delete_submit_track_and_view_permissions()
    {
        var character = NewCharacter(ownerUserId: _owner);

        character.Character.EnsureCanDelete(_owner, false);
        character.Character.EnsureCanSubmit(_owner);
        character.EnsureCanTrack(_owner, false);
        character.EnsureCanTrack(_other, true);
        Assert.True(character.CanViewSheet(_owner, false));
        Assert.False(character.CanViewSheet(_other, false));
        Assert.Throws<DomainException>(() => character.Character.EnsureCanSubmit(_other));
        Assert.Throws<DomainException>(() => character.EnsureCanTrack(_other, false));
        Assert.Throws<DomainException>(() => character.Character.EnsureCanDelete(_other, false));

        character.Activate(10, Now);

        Assert.Equal(DomainErrorKind.Forbidden, Assert.Throws<DomainException>(() => character.Character.EnsureCanDelete(_owner, false)).Kind);
        character.Character.EnsureCanDelete(_other, true);
        Assert.Equal(DomainErrorKind.Conflict, Assert.Throws<DomainException>(() => character.Character.EnsureCanSubmit(_owner)).Kind);
    }

    [Fact]
    public void Combat_update_applies_given_fields_and_replaces_conditions()
    {
        var character = NewCharacter();
        character.ApplyCombatUpdate(new CombatUpdate { Conditions = [new CharacterCondition("prone")], Inspiration = true }, 10, Now);

        character.ApplyCombatUpdate(
            new CombatUpdate { HitPointsCurrent = 4, TemporaryHitPoints = 3, DeathSaveFailures = 1, ExhaustionLevel = 2, Conditions = [new CharacterCondition(" poisoned ", "Veneno de araña")] },
            10,
            Now);

        Assert.Equal((4, 3, 0, 1, 2, true), (character.HitPointsCurrent, character.TemporaryHitPoints, character.DeathSaveSuccesses, character.DeathSaveFailures, character.ExhaustionLevel, character.Inspiration));
        Assert.Equal([new CharacterCondition("poisoned", "Veneno de araña")], character.Conditions);
        Assert.Contains("\"index\":\"poisoned\"", character.ConditionsJson);
    }

    [Theory]
    [MemberData(nameof(InvalidCombatUpdates))]
    public void Combat_update_validates_ranges(CombatUpdate update)
    {
        var character = NewCharacter();

        Assert.Throws<DomainException>(() => character.ApplyCombatUpdate(update, 10, Now));
    }

    public static TheoryData<CombatUpdate> InvalidCombatUpdates() => new()
    {
        new CombatUpdate { HitPointsCurrent = 11 },
        new CombatUpdate { HitPointsCurrent = -1 },
        new CombatUpdate { TemporaryHitPoints = -1 },
        new CombatUpdate { DeathSaveSuccesses = 4 },
        new CombatUpdate { DeathSaveFailures = -1 },
        new CombatUpdate { ExhaustionLevel = 7 },
        new CombatUpdate { Conditions = [new CharacterCondition("prone"), new CharacterCondition("prone")] },
        new CombatUpdate { Conditions = [new CharacterCondition("")] },
    };

    [Fact]
    public void Concentration_is_set_and_cleared()
    {
        var character = NewCharacter();

        character.SetConcentration("bless", Now);
        Assert.Equal("bless", character.ConcentratingOnSpellIndex);

        character.SetConcentration(null, Now);
        Assert.Null(character.ConcentratingOnSpellIndex);
    }

    [Fact]
    public void Spell_slots_cannot_be_spent_beyond_the_maximum_nor_restored_beyond_the_spent()
    {
        var character = NewCharacter();

        var slot = character.SpendSpellSlot(1, 2, maxSlots: 3, Now);
        Assert.Equal((1, 2), (slot.Level, slot.Used));
        Assert.Throws<DomainException>(() => character.SpendSpellSlot(1, 2, 3, Now));
        Assert.Throws<DomainException>(() => character.SpendSpellSlot(2, 1, 0, Now));
        Assert.Throws<DomainException>(() => character.SpendSpellSlot(10, 1, 3, Now));
        Assert.Throws<DomainException>(() => character.SpendSpellSlot(1, 0, 3, Now));

        character.RestoreSpellSlot(1, 1, Now);
        Assert.Equal(1, character.SpellSlotsUsed(1));
        Assert.Throws<DomainException>(() => character.RestoreSpellSlot(1, 2, Now));
        Assert.Single(character.SpellSlots);
    }

    [Fact]
    public void Resources_are_spent_restored_and_only_manual_ones_removed()
    {
        var character = NewCharacter(classes: [new ClassEntry("barbarian", null, 1)]);
        character.SyncAutoResources(ClassResourceRules.ForClasses(character.Classes, Sheet(character).AbilityModifiers));
        var rage = character.Resources.Single();
        var wand = character.AddManualResource("  Varita  ", 3, ResourceRecharge.Dawn, Now);

        character.SpendResource(rage.Id, 2, Now);
        Assert.Throws<DomainException>(() => character.SpendResource(rage.Id, 1, Now));
        character.RestoreResource(rage.Id, 1, Now);
        Assert.Equal(1, rage.Used);
        Assert.Throws<DomainException>(() => character.RestoreResource(wand.Id, 1, Now));

        Assert.Equal(("Varita", false, (string?)null), (wand.Name, wand.IsAuto, wand.Key));
        Assert.Throws<DomainException>(() => character.RemoveManualResource(rage.Id, Now));
        character.RemoveManualResource(wand.Id, Now);
        Assert.Equal(DomainErrorKind.NotFound, Assert.Throws<DomainException>(() => character.SpendResource(wand.Id, 1, Now)).Kind);
    }

    [Theory]
    [InlineData("", 1)]
    [InlineData("Varita", 0)]
    [InlineData("Varita", 1000)]
    public void Manual_resources_validate_name_and_max(string name, int max)
    {
        var character = NewCharacter();

        Assert.Throws<DomainException>(() => character.AddManualResource(name, max, ResourceRecharge.LongRest, Now));
    }
}
