using OpenTrpg.Core.Domain.Catalog;
using OpenTrpg.Core.Domain.Characters;

namespace OpenTrpg.Core.Domain.Tests.Characters;

/// <summary>SRD 5.1 catalog data needed by the character tests.</summary>
internal static class TestCatalog
{
    public static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    // Full caster table (identical to the multiclass table), wizard/cleric/bard...
    private static readonly int[][] FullCaster =
    [
        [2, 0, 0, 0, 0, 0, 0, 0, 0], [3, 0, 0, 0, 0, 0, 0, 0, 0], [4, 2, 0, 0, 0, 0, 0, 0, 0], [4, 3, 0, 0, 0, 0, 0, 0, 0],
        [4, 3, 2, 0, 0, 0, 0, 0, 0], [4, 3, 3, 0, 0, 0, 0, 0, 0], [4, 3, 3, 1, 0, 0, 0, 0, 0], [4, 3, 3, 2, 0, 0, 0, 0, 0],
        [4, 3, 3, 3, 1, 0, 0, 0, 0], [4, 3, 3, 3, 2, 0, 0, 0, 0], [4, 3, 3, 3, 2, 1, 0, 0, 0], [4, 3, 3, 3, 2, 1, 0, 0, 0],
        [4, 3, 3, 3, 2, 1, 1, 0, 0], [4, 3, 3, 3, 2, 1, 1, 0, 0], [4, 3, 3, 3, 2, 1, 1, 1, 0], [4, 3, 3, 3, 2, 1, 1, 1, 0],
        [4, 3, 3, 3, 2, 1, 1, 1, 1], [4, 3, 3, 3, 3, 1, 1, 1, 1], [4, 3, 3, 3, 3, 2, 1, 1, 1], [4, 3, 3, 3, 3, 2, 2, 1, 1],
    ];

    private static readonly int[][] HalfCaster =
    [
        [0, 0, 0, 0, 0, 0, 0, 0, 0], [2, 0, 0, 0, 0, 0, 0, 0, 0], [3, 0, 0, 0, 0, 0, 0, 0, 0], [3, 0, 0, 0, 0, 0, 0, 0, 0],
        [4, 2, 0, 0, 0, 0, 0, 0, 0], [4, 2, 0, 0, 0, 0, 0, 0, 0], [4, 3, 0, 0, 0, 0, 0, 0, 0], [4, 3, 0, 0, 0, 0, 0, 0, 0],
        [4, 3, 2, 0, 0, 0, 0, 0, 0], [4, 3, 2, 0, 0, 0, 0, 0, 0], [4, 3, 3, 0, 0, 0, 0, 0, 0], [4, 3, 3, 0, 0, 0, 0, 0, 0],
        [4, 3, 3, 1, 0, 0, 0, 0, 0], [4, 3, 3, 1, 0, 0, 0, 0, 0], [4, 3, 3, 2, 0, 0, 0, 0, 0], [4, 3, 3, 2, 0, 0, 0, 0, 0],
        [4, 3, 3, 3, 1, 0, 0, 0, 0], [4, 3, 3, 3, 1, 0, 0, 0, 0], [4, 3, 3, 3, 2, 0, 0, 0, 0], [4, 3, 3, 3, 2, 0, 0, 0, 0],
    ];

    // Warlock levels 1-5 (enough for the tests).
    private static readonly int[][] Pact =
    [
        [1, 0, 0, 0, 0, 0, 0, 0, 0], [2, 0, 0, 0, 0, 0, 0, 0, 0], [0, 2, 0, 0, 0, 0, 0, 0, 0], [0, 2, 0, 0, 0, 0, 0, 0, 0],
        [0, 0, 2, 0, 0, 0, 0, 0, 0],
    ];

    public static ClassInfo Barbarian { get; } = Class("barbarian", 12);

    public static ClassInfo Fighter { get; } = Class("fighter", 10);

    public static ClassInfo Monk { get; } = Class("monk", 8);

    public static ClassInfo Rogue { get; } = Class("rogue", 8);

    public static ClassInfo Wizard { get; } = Class("wizard", 6, "int", 1, FullCaster);

    public static ClassInfo Cleric { get; } = Class("cleric", 8, "wis", 1, FullCaster);

    public static ClassInfo Druid { get; } = Class("druid", 8, "wis", 1, FullCaster);

    public static ClassInfo Paladin { get; } = Class("paladin", 10, "cha", 2, HalfCaster);

    public static ClassInfo Warlock { get; } = Class("warlock", 8, "cha", 1, Pact, isPact: true);

    public static IReadOnlyList<ClassInfo> Classes { get; } = [Barbarian, Fighter, Monk, Rogue, Wizard, Cleric, Druid, Paladin, Warlock];

    public static IReadOnlyList<SkillInfo> Skills { get; } =
    [
        new("acrobatics", "Acrobatics", "dex"),
        new("athletics", "Athletics", "str"),
        new("perception", "Perception", "wis"),
        new("stealth", "Stealth", "dex"),
    ];

    public static RaceInfo Dwarf { get; } = new(25, [new AbilityBonus("con", 2)]);

    public static SubraceInfo HillDwarf { get; } = new([new AbilityBonus("wis", 1)]);

    public static Character NewCharacter(
        AbilityScores? abilities = null,
        IReadOnlyList<ClassEntry>? classes = null,
        IReadOnlyList<ProficiencyEntry>? proficiencies = null,
        IReadOnlyList<OverrideEntry>? overrides = null,
        Guid? ownerUserId = null)
    {
        var character = Character.Create(Guid.NewGuid(), ownerUserId ?? Guid.NewGuid(), "Test", Now);
        character.ApplySheetEdit(
            new SheetEdit
            {
                BaseAbilities = abilities,
                Classes = classes,
                Proficiencies = proficiencies,
                Overrides = overrides,
            },
            Now);
        return character;
    }

    public static CharacterSheet Sheet(Character character, RaceInfo? race = null, SubraceInfo? subrace = null, EquippedGear? gear = null) =>
        SheetCalculator.Calculate(new SheetInput(character, Classes, race, subrace, Skills, gear));

    public static AbilityScores Scores(int str = 10, int dex = 10, int con = 10, int @int = 10, int wis = 10, int cha = 10) =>
        new(str, dex, con, @int, wis, cha);

    private static ClassInfo Class(string index, int hitDie, string? ability = null, int spellcastingLevel = 0, int[][]? table = null, bool isPact = false) => new()
    {
        Index = index,
        HitDie = hitDie,
        SpellcastingAbility = ability,
        SpellcastingLevel = spellcastingLevel,
        IsPactCaster = isPact,
        SpellSlotsByLevel = (table ?? []).Select((slots, i) => (Level: i + 1, Slots: (IReadOnlyList<int>)slots)).ToDictionary(x => x.Level, x => x.Slots),
    };
}

/// <summary>Rolls the values given, in order (cycling).</summary>
internal sealed class FixedDice(params int[] rolls) : IDiceRoller
{
    private int _next;

    public List<int> Sides { get; } = [];

    public int Roll(int sides)
    {
        Sides.Add(sides);
        return rolls[_next++ % rolls.Length];
    }
}
