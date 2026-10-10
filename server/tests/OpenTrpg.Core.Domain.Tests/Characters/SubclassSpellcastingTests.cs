using OpenTrpg.Core.Domain.Catalog;
using OpenTrpg.Core.Domain.Characters;
using static OpenTrpg.Core.Domain.Tests.Characters.TestCatalog;

namespace OpenTrpg.Core.Domain.Tests.Characters;

/// <summary>Spellcasting given by a subclass to a class that does not cast (content packs, phase 25 block 3).</summary>
public class SubclassSpellcastingTests
{
    private static readonly SubclassSpellcasting Runes = new(
        3,
        "int",
        3,
        "wizard",
        new Dictionary<int, int> { [3] = 2, [10] = 3 },
        new Dictionary<int, int> { [3] = 3, [4] = 4, [7] = 5 });

    private static CharacterSheet Sheet(Dnd5eCharacter character, SubclassSpellcasting? casting = null)
    {
        var classes = Classes.Select(c => c.Index == "fighter" ? c.WithSubclassSpellcasting(casting ?? Runes) : c).ToList();
        return SheetCalculator.Calculate(new SheetInput(character, classes, null, null, Skills));
    }

    [Theory]
    [InlineData(1, new[] { 0, 0, 0, 0 })]
    [InlineData(2, new[] { 0, 0, 0, 0 })]
    [InlineData(3, new[] { 2, 0, 0, 0 })]
    [InlineData(4, new[] { 3, 0, 0, 0 })]
    [InlineData(7, new[] { 4, 2, 0, 0 })]
    [InlineData(10, new[] { 4, 3, 0, 0 })]
    [InlineData(13, new[] { 4, 3, 2, 0 })]
    [InlineData(16, new[] { 4, 3, 3, 0 })]
    [InlineData(19, new[] { 4, 3, 3, 1 })]
    [InlineData(20, new[] { 4, 3, 3, 1 })]
    public void Single_class_third_caster_uses_the_third_caster_table(int level, int[] expected)
    {
        var character = NewCharacter(Scores(@int: 14), [new ClassEntry("fighter", "runas", level)]);

        var sheet = Sheet(character);

        Assert.Equal([.. expected, 0, 0, 0, 0, 0], sheet.SpellSlotsMax);
    }

    [Fact]
    public void Below_its_first_level_the_class_does_not_cast()
    {
        var character = NewCharacter(Scores(@int: 14), [new ClassEntry("fighter", null, 2)]);

        Assert.Empty(Sheet(character).Spellcasting);
    }

    [Fact]
    public void Spell_dc_and_attack_use_the_subclass_ability_with_breakdown()
    {
        var character = NewCharacter(Scores(@int: 16, wis: 18), [new ClassEntry("fighter", "runas", 5)]);

        var sheet = Sheet(character);

        var casting = Assert.Single(sheet.Spellcasting);
        Assert.Equal(new SpellcastingValue("fighter", "int", 8 + 3 + 3, 3 + 3, null) { MaxSpellLevel = 1, SpellsKnownMax = 4, CantripsKnownMax = 2 }, casting);
        Assert.Equal(
            [("base", 8), ("proficiency", 3), ("ability", 3)],
            sheet.Breakdowns["spellSaveDc.fighter"].Parts.Select(p => (p.Source, p.Value)));
        Assert.Equal("Inteligencia", sheet.Breakdowns["spellAttackBonus.fighter"].Parts.Single(p => p.Source == "ability").Label);
    }

    [Fact]
    public void In_a_multiclass_the_third_caster_adds_a_third_of_its_level()
    {
        // Fighter 7 → floor(7/3) = 2, wizard 3 → 3: caster level 5.
        var character = NewCharacter(Scores(@int: 14), [new ClassEntry("fighter", "runas", 7), new ClassEntry("wizard", null, 3)]);

        var sheet = Sheet(character);

        Assert.Equal(5, sheet.MulticlassCasterLevel);
        Assert.Equal(SpellSlotTables.MulticlassSlots(5), sheet.SpellSlotsMax);
        Assert.Equal(["fighter", "wizard"], sheet.Spellcasting.Select(s => s.ClassIndex));
    }

    [Fact]
    public void Half_and_full_progressions_use_their_tables_and_divisors()
    {
        var half = Runes with { SpellcastingLevel = 2, FromLevel = 2 };
        Assert.Equal([3, 0, 0, 0, 0, 0, 0, 0, 0], Sheet(NewCharacter(classes: [new ClassEntry("fighter", "x", 3)]), half).SpellSlotsMax);
        var multiclass = NewCharacter(classes: [new ClassEntry("fighter", "x", 6), new ClassEntry("wizard", null, 1)]);
        Assert.Equal(3 + 1, Sheet(multiclass, half).MulticlassCasterLevel);

        var full = Runes with { SpellcastingLevel = 1, FromLevel = 1 };
        Assert.Equal(SpellSlotTables.MulticlassSlots(5), Sheet(NewCharacter(classes: [new ClassEntry("fighter", "x", 5)]), full).SpellSlotsMax);
    }

    [Fact]
    public void A_class_that_casts_on_its_own_ignores_the_subclass_spellcasting()
    {
        Assert.Same(Wizard, Wizard.WithSubclassSpellcasting(Runes));
        Assert.Same(Fighter, Fighter.WithSubclassSpellcasting(null));
    }

    [Fact]
    public void Known_tables_take_the_highest_entry_not_above_the_level()
    {
        Assert.Equal((0, 3, 4, 4, 5), (Runes.SpellsKnownAt(2) ?? -1, Runes.SpellsKnownAt(3) ?? -1, Runes.SpellsKnownAt(4) ?? -1, Runes.SpellsKnownAt(6) ?? -1, Runes.SpellsKnownAt(20) ?? -1));
        Assert.Null((Runes with { CantripsKnown = new Dictionary<int, int>() }).CantripsKnownAt(5));
    }

    [Fact]
    public void Stored_json_round_trips()
    {
        var parsed = SubclassSpellcasting.Parse(Runes.ToJson())!;

        Assert.Equal(("third", "int", 3, "wizard"), (parsed.Progression, parsed.Ability, parsed.FromLevel, parsed.SpellList));
        Assert.Equal(Runes.SpellsKnown, parsed.SpellsKnown);
        Assert.Equal(Runes.CantripsKnown, parsed.CantripsKnown);
        Assert.Null(SubclassSpellcasting.Parse("""{"progression":"quarter","ability":"int","spellList":"wizard"}"""));
        Assert.Null(SubclassSpellcasting.Parse("not json"));
    }

    [Fact]
    public void School_filter_allows_the_listed_schools_and_any_school_at_the_exception_levels()
    {
        var filter = LevelChoiceJson.ParseFilter("""{"schools":["Abjuration","evocation"],"schoolsExceptAt":[20,3,8,14]}""");

        Assert.Equal(["abjuration", "evocation"], filter.Schools);
        Assert.Equal([3, 8, 14, 20], filter.SchoolsExceptAt);
        Assert.True(filter.AllowsSchool("Evocation", 4));
        Assert.False(filter.AllowsSchool("Enchantment", 4));
        Assert.True(filter.AllowsSchool("Enchantment", 8));
        Assert.Equal("Solo abjuración o evocación salvo en los niveles 3, 8, 14 y 20", filter.SchoolsReason());
        Assert.Equal("Solo ilusión", (ChoiceFilter.None with { Schools = ["illusion"] }).SchoolsReason());
        Assert.True(ChoiceFilter.None.AllowsSchool("Necromancy", 1));
    }
}
