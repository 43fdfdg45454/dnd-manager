namespace Dnd.Domain.Characters;

/// <summary>One class of a character, as sent in a sheet edit. The list order sets <see cref="CharacterClassLevel.Order"/>.</summary>
public sealed record ClassEntry(string ClassIndex, string? SubclassIndex, int Level);

public sealed record ProficiencyEntry(ProficiencyType Type, string Key, bool Expertise = false, ProficiencySource Source = ProficiencySource.Manual);

public sealed record SpellEntry(string SpellIndex, string ClassIndex, bool IsPrepared, bool AlwaysPrepared = false);

public sealed record OverrideEntry(string Field, int Value, string? Note = null);

/// <summary>
/// A sheet edit (the domain side of <c>SheetPatch</c>), applied by <see cref="Character.ApplySheetEdit"/>
/// directly or when a change request is approved. Null means "unchanged". For the optional indexes
/// (race, subrace, background, alignment) an empty string clears the value. Every list given replaces
/// the existing one completely.
/// </summary>
public sealed record SheetEdit
{
    public string? Name { get; init; }

    /// <summary>Changing the race without giving <see cref="SubraceIndex"/> clears the subrace.</summary>
    public string? RaceIndex { get; init; }

    public string? SubraceIndex { get; init; }

    public string? BackgroundIndex { get; init; }

    public string? Alignment { get; init; }

    public bool? ApplyRacialBonuses { get; init; }

    public HpMode? HpMode { get; init; }

    public AbilityScores? BaseAbilities { get; init; }

    public IReadOnlyList<ClassEntry>? Classes { get; init; }

    public IReadOnlyList<ProficiencyEntry>? Proficiencies { get; init; }

    public IReadOnlyList<SpellEntry>? Spells { get; init; }

    /// <summary>
    /// The edit comes from the owner of an active character (an approved change request): spells the character
    /// already has keep their <see cref="SpellEntry.IsPrepared"/> (the owner prepares spells with the spell
    /// preparation operation, not with sheet edits). DMs change it freely.
    /// </summary>
    public bool KeepSpellPreparation { get; init; }

    public IReadOnlyList<OverrideEntry>? Overrides { get; init; }

    public string? Notes { get; init; }

    public string? Backstory { get; init; }

    public string? PersonalityTraits { get; init; }

    public string? Ideals { get; init; }

    public string? Bonds { get; init; }

    public string? Flaws { get; init; }

    public string? BackgroundDetail { get; init; }

    public int? CopperPieces { get; init; }

    /// <summary>Height in inches; 0 clears it.</summary>
    public int? HeightInches { get; init; }

    /// <summary>Weight in pounds; 0 clears it.</summary>
    public int? WeightPounds { get; init; }
}
