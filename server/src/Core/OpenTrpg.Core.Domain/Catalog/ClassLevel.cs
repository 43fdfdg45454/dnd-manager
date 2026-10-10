namespace OpenTrpg.Core.Domain.Catalog;

/// <summary>Class progression for one level. <see cref="Index"/> is the dataset slug, e.g. "wizard-1".</summary>
public sealed class ClassLevel
{
    public const int SpellSlotLevels = 9;

    public required string Index { get; init; }

    public required string ClassIndex { get; init; }

    public int Level { get; init; }

    public int ProfBonus { get; init; }

    /// <summary>Ability Score Improvements gained up to this level (cumulative).</summary>
    public int AbilityScoreBonuses { get; init; }

    public IReadOnlyList<string> FeatureIndexes { get; init; } = [];

    /// <summary>Class-specific counters of the dataset (rage count, sneak attack dice, ...) as a JSON object.</summary>
    public string ClassSpecificJson { get; init; } = "{}";

    public int? CantripsKnown { get; init; }

    public int? SpellsKnown { get; init; }

    /// <summary>Spell slots per spell level 1..9 (always 9 entries; zeros for non-casters).</summary>
    public IReadOnlyList<int> SpellSlots { get; init; } = new int[SpellSlotLevels];
}
