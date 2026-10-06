using Dnd.Domain.Items;

namespace Dnd.Domain.Characters;

/// <summary>Final ability score (base + racial bonuses, or the override) and its modifier.</summary>
public sealed record AbilityValue(int Score, int Modifier, bool Overridden);

public sealed record SavingThrowValue(int Value, bool Proficient, bool Overridden);

public sealed record SkillValue(string Index, string Name, string Ability, int Value, bool Proficient, bool Expertise, bool Overridden);

/// <summary>Hit dice of one class: <see cref="Total"/> = class level, <see cref="Remaining"/> = level minus spent.</summary>
public sealed record HitDiceValue(string ClassIndex, int Die, int Total, int Remaining);

/// <summary>Spellcasting numbers of one class. <see cref="PreparedMax"/> only for classes that prepare spells.</summary>
public sealed record SpellcastingValue(string ClassIndex, string Ability, int SaveDc, int AttackBonus, int? PreparedMax);

/// <summary>Warlock Pact Magic: <see cref="Slots"/> slots, all of spell level <see cref="SlotLevel"/>.</summary>
public sealed record PactMagicValue(int SlotLevel, int Slots);

/// <summary>
/// An item modifier applied to the sheet, for the UI to mark the values affected by items. Attack and
/// damage bonuses of non-weapon items are listed too (they apply to every attack).
/// </summary>
public sealed record AppliedItemEffect(string ItemName, ItemModifierKind Kind, string? Target, int Value);

/// <summary>Calculated character sheet produced by <see cref="SheetCalculator.Calculate"/>.</summary>
public sealed record CharacterSheet
{
    /// <summary>By ability index, in sheet order (str, dex, con, int, wis, cha).</summary>
    public required IReadOnlyDictionary<string, AbilityValue> Abilities { get; init; }

    /// <summary>Sum of class levels (0 without classes).</summary>
    public required int TotalLevel { get; init; }

    public required int ProficiencyBonus { get; init; }

    /// <summary>By ability index, in sheet order.</summary>
    public required IReadOnlyDictionary<string, SavingThrowValue> SavingThrows { get; init; }

    /// <summary>In the order of <see cref="SheetInput.Skills"/>.</summary>
    public required IReadOnlyList<SkillValue> Skills { get; init; }

    public required int PassivePerception { get; init; }

    public required int Initiative { get; init; }

    public required int ArmorClass { get; init; }

    public required int Speed { get; init; }

    public required int HitPointsMax { get; init; }

    /// <summary>One entry per class, main class first.</summary>
    public required IReadOnlyList<HitDiceValue> HitDice { get; init; }

    /// <summary>One entry per class with a spellcasting ability, main class first (warlock included).</summary>
    public required IReadOnlyList<SpellcastingValue> Spellcasting { get; init; }

    /// <summary>Spellcaster level used for the multiclass table (0 when a single class table applies or no casting).</summary>
    public required int MulticlassCasterLevel { get; init; }

    /// <summary>Maximum regular slots for spell levels 1-9 (always 9 entries; pact slots excluded).</summary>
    public required IReadOnlyList<int> SpellSlotsMax { get; init; }

    /// <summary>Pact Magic slots, or null without warlock levels (or none at that level).</summary>
    public required PactMagicValue? PactMagic { get; init; }

    /// <summary>Fields replaced by an override (<see cref="OverrideFields"/>), sorted.</summary>
    public required IReadOnlyList<string> OverriddenFields { get; init; }

    /// <summary>
    /// Modifiers of active items applied to the sheet, in inventory order. Excludes the attack and damage
    /// bonuses of weapons (they apply to their own attack) and ability sets that did not raise the score.
    /// </summary>
    public IReadOnlyList<AppliedItemEffect> ItemEffects { get; init; } = [];

    /// <summary>
    /// How every value was obtained, point by point, keyed like <see cref="OverrideFields"/>:
    /// "ability.str", "save.dex", "skill.stealth", "armorClass", "initiative", "speed", "hitPointsMax",
    /// "passivePerception", "proficiencyBonus", plus "spellSaveDc.&lt;classIndex&gt;" and
    /// "spellAttackBonus.&lt;classIndex&gt;" per spellcasting class.
    /// </summary>
    public IReadOnlyDictionary<string, ValueBreakdown> Breakdowns { get; init; } = new Dictionary<string, ValueBreakdown>();

    /// <summary>Modifier of each ability by index; input for <see cref="ClassResourceRules"/>.</summary>
    public IReadOnlyDictionary<string, int> AbilityModifiers => Abilities.ToDictionary(a => a.Key, a => a.Value.Modifier);

    public int Modifier(string ability) => Abilities[ability].Modifier;

    /// <summary>
    /// Maximum slots for a level as stored in <see cref="SpellSlotState"/>: 1-9 regular slots,
    /// <see cref="SpellSlotState.PactLevel"/> (0) pact slots. Out-of-range levels have 0.
    /// </summary>
    public int SpellSlotMax(int level) => level switch
    {
        SpellSlotState.PactLevel => PactMagic?.Slots ?? 0,
        >= 1 and <= 9 => SpellSlotsMax[level - 1],
        _ => 0,
    };

    public bool IsOverridden(string field) => OverriddenFields.Contains(field, StringComparer.Ordinal);
}
