using OpenTrpg.Core.Domain.Catalog;
using OpenTrpg.Core.Domain.Characters;
using OpenTrpg.Core.Domain.Common;
using static OpenTrpg.Systems.Dnd5e.Domain.Tests.Characters.TestCatalog;
using OpenTrpg.Systems.Dnd5e.Domain.Catalog;
using OpenTrpg.Systems.Dnd5e.Domain.Characters;

namespace OpenTrpg.Systems.Dnd5e.Domain.Tests.Characters;

/// <summary>Spell preparation (phase 18): who prepares, when it becomes pending and how it is replaced.</summary>
public class SpellPreparationTests
{
    [Fact]
    public void Cleric_1_with_Wis_16_prepares_up_to_4_spells_of_level_1()
    {
        var sheet = Sheet(NewCharacter(Scores(wis: 16), [new ClassEntry("cleric", null, 1)]));

        var cleric = Assert.Single(sheet.PreparingClasses);
        Assert.Equal((4, 1), (cleric.PreparedMax, cleric.MaxSpellLevel));
    }

    [Fact]
    public void A_paladin_starts_preparing_at_level_2_and_non_preparers_never_do()
    {
        Assert.Empty(Sheet(NewCharacter(Scores(cha: 14), [new ClassEntry("paladin", null, 1)])).PreparingClasses);
        var paladin = Assert.Single(Sheet(NewCharacter(Scores(cha: 14), [new ClassEntry("paladin", null, 2)])).PreparingClasses);
        Assert.Equal((3, 1), (paladin.PreparedMax, paladin.MaxSpellLevel));
        Assert.Empty(Sheet(NewCharacter(Scores(cha: 16), [new ClassEntry("warlock", null, 3)])).PreparingClasses);
        Assert.Empty(Sheet(NewCharacter(Scores(), [new ClassEntry("fighter", null, 5)])).PreparingClasses);
    }

    [Fact]
    public void A_multiclassed_caster_prepares_each_class_with_its_own_table()
    {
        var sheet = Sheet(NewCharacter(Scores(@int: 14, cha: 16), [new ClassEntry("paladin", null, 4), new ClassEntry("wizard", null, 2)]));

        Assert.Equal([("paladin", 1), ("wizard", 1)], sheet.PreparingClasses.Select(c => (c.ClassIndex, c.MaxSpellLevel)));
    }

    [Fact]
    public void A_long_rest_asks_casters_that_prepare_to_prepare_again()
    {
        var cleric = NewCharacter(Scores(wis: 14), [new ClassEntry("cleric", null, 3)]);
        var fighter = NewCharacter(Scores(), [new ClassEntry("fighter", null, 3)]);

        cleric.LongRest(Sheet(cleric), Now);
        fighter.LongRest(Sheet(fighter), Now);

        Assert.True(cleric.SpellPreparationPending);
        Assert.Equal(SpellPreparationReason.LongRest, cleric.SpellPreparationReason);
        Assert.False(fighter.SpellPreparationPending);
        Assert.Null(fighter.SpellPreparationReason);

        Assert.True(cleric.CompleteSpellPreparation(Now));
        Assert.False(cleric.CompleteSpellPreparation(Now));
        Assert.Null(cleric.SpellPreparationReason);
    }

    [Fact]
    public void Preparing_a_cleric_replaces_the_list_and_leaves_cantrips_and_always_prepared_spells_alone()
    {
        var cleric = NewCharacter(Scores(wis: 16), [new ClassEntry("cleric", null, 1)]);
        cleric.ReplaceSpells(
            [
                new SpellEntry("sacred-flame", "cleric", true),
                new SpellEntry("bless", "cleric", true, AlwaysPrepared: true),
                new SpellEntry("cure-wounds", "cleric", true),
                new SpellEntry("guiding-bolt", "cleric", true),
            ],
            Now);

        cleric.SetPreparedSpells("cleric", ["guiding-bolt", "healing-word"], IsLeveled, keepUnprepared: false, Now);

        Assert.Equal(
            [("bless", true, true), ("guiding-bolt", true, false), ("healing-word", true, false), ("sacred-flame", true, false)],
            cleric.Spells.Select(s => (s.SpellIndex, s.IsPrepared, s.AlwaysPrepared)).OrderBy(s => s.SpellIndex));
    }

    [Fact]
    public void Preparing_a_wizard_keeps_the_spellbook()
    {
        var wizard = NewCharacter(Scores(@int: 16), [new ClassEntry("wizard", null, 1)]);
        wizard.ReplaceSpells([new SpellEntry("magic-missile", "wizard", true), new SpellEntry("shield", "wizard", false), new SpellEntry("sleep", "wizard", false)], Now);

        wizard.SetPreparedSpells("wizard", ["shield", "sleep"], IsLeveled, keepUnprepared: true, Now);

        Assert.Equal(
            [("magic-missile", false), ("shield", true), ("sleep", true)],
            wizard.Spells.Select(s => (s.SpellIndex, s.IsPrepared)).OrderBy(s => s.SpellIndex));
        Assert.Throws<DomainException>(() => wizard.SetPreparedSpells("cleric", [], IsLeveled, false, Now));
    }

    [Fact]
    public void A_sheet_edit_of_the_owner_keeps_the_preparation_but_a_dm_edit_changes_it()
    {
        var wizard = NewCharacter(Scores(@int: 16), [new ClassEntry("wizard", null, 1)]);
        wizard.ReplaceSpells([new SpellEntry("shield", "wizard", false), new SpellEntry("sleep", "wizard", true)], Now);
        List<SpellEntry> edited = [new("shield", "wizard", true), new("sleep", "wizard", false), new("mage-armor", "wizard", false)];

        wizard.ApplySheetEdit(new SheetEdit { Spells = edited, KeepSpellPreparation = true }, Now);

        Assert.Equal([("mage-armor", false), ("shield", false), ("sleep", true)], wizard.Spells.Select(s => (s.SpellIndex, s.IsPrepared)).OrderBy(s => s.SpellIndex));

        wizard.ApplySheetEdit(new SheetEdit { Spells = edited }, Now);

        Assert.Equal([("mage-armor", false), ("shield", true), ("sleep", false)], wizard.Spells.Select(s => (s.SpellIndex, s.IsPrepared)).OrderBy(s => s.SpellIndex));
    }

    [Theory]
    [InlineData(true, true, true, SpellCategory.Healing)]
    [InlineData(false, true, true, SpellCategory.Damage)]
    [InlineData(false, false, true, SpellCategory.Control)]
    [InlineData(false, false, false, SpellCategory.Utility)]
    public void The_category_is_derived_from_healing_damage_and_saving_throw(bool heals, bool damage, bool save, SpellCategory expected) =>
        Assert.Equal(expected, SpellCategories.Derive(heals, damage, save));

    [Theory]
    [InlineData("buff", SpellCategory.Buff)]
    [InlineData(" Summoning ", SpellCategory.Summoning)]
    [InlineData("DEFENSE", SpellCategory.Defense)]
    [InlineData("3", null)]
    [InlineData("magic", null)]
    [InlineData("", null)]
    public void Category_names_parse_ignoring_case(string value, SpellCategory? expected) =>
        Assert.Equal(expected, SpellCategories.TryParse(value));

    private static bool IsLeveled(string spellIndex) => spellIndex != "sacred-flame";
}
