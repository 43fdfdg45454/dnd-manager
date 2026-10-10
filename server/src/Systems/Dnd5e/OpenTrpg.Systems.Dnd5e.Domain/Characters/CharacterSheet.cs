using OpenTrpg.Core.Domain.Items;
using OpenTrpg.Core.Domain.Characters;
using OpenTrpg.Systems.Dnd5e.Domain.Catalog;
using OpenTrpg.Systems.Dnd5e.Domain.Characters;

namespace OpenTrpg.Systems.Dnd5e.Domain.Characters;

/// <summary>Final ability score (base + racial bonuses, or the override) and its modifier.</summary>
public sealed record AbilityValue(int Score, int Modifier, bool Overridden);

public sealed record SavingThrowValue(int Value, bool Proficient, bool Overridden);

public sealed record SkillValue(string Index, string Name, string Ability, int Value, bool Proficient, bool Expertise, bool Overridden);

/// <summary>Hit dice of one class: <see cref="Total"/> = class level, <see cref="Remaining"/> = level minus spent.</summary>
public sealed record HitDiceValue(string ClassIndex, int Die, int Total, int Remaining);

/// <summary>Spellcasting numbers of one class. <see cref="PreparedMax"/> only for classes that prepare spells.</summary>
public sealed record SpellcastingValue(string ClassIndex, string Ability, int SaveDc, int AttackBonus, int? PreparedMax)
{
    /// <summary>
    /// Highest spell level with slots in the class's own table at its level (0 without slots, e.g. a level 1
    /// paladin). Multiclassed characters prepare each class as if single-classed (PHB, multiclassing).
    /// </summary>
    public int MaxSpellLevel { get; init; }

    /// <summary>Spells known at the class level when the class learns a fixed number (subclass casters); null otherwise.</summary>
    public int? SpellsKnownMax { get; init; }

    /// <summary>Cantrips known at the class level when the class learns a fixed number (subclass casters); null otherwise.</summary>
    public int? CantripsKnownMax { get; init; }

    /// <summary>Progression of a pack class ("hitPoints", "ritualsOnly"...); null for the SRD classes and subclass casters.</summary>
    public string? Progression { get; init; }

    /// <summary>"hitPoints": hit points lost by spell level (index 0 = 1st level), up to <see cref="MaxSpellLevel"/>.</summary>
    public IReadOnlyList<int> HitPointCost { get; init; } = Array.Empty<int>();

    /// <summary>"hitPoints": key of the class resource each spell spends.</summary>
    public string? UsesResource { get; init; }

    /// <summary>The class prepares spells (cleric, druid, paladin, wizard) and can already cast leveled spells.</summary>
    public bool PreparesSpells => PreparedMax is not null && MaxSpellLevel > 0;

    /// <summary>Save DC of the cantrips when a feature changes it (breakdown "spellSaveDc.&lt;class&gt;.cantrip"); null otherwise.</summary>
    public int? CantripSaveDc { get; init; }

    /// <summary>Spell attack bonus of the cantrips when a feature changes it ("spellAttackBonus.&lt;class&gt;.cantrip"); null otherwise.</summary>
    public int? CantripAttackBonus { get; init; }

    /// <summary>Lowest d20 roll of a spell attack that is a critical hit (20 by default).</summary>
    public int CriticalRange { get; init; } = SheetCalculator.DefaultCriticalRange;

    /// <summary>What lowered <see cref="CriticalRange"/> ("Crítico afinado (nivel 3)"), or null.</summary>
    public string? CriticalRangeSource { get; init; }
}

/// <summary>A save DC of a class besides the spell one ("CD de bombas"): breakdown "classSaveDc.&lt;class&gt;.&lt;n&gt;".</summary>
public sealed record ClassSaveDcValue(string ClassIndex, string Name, string Ability, int Dc, string BreakdownKey);

/// <summary>
/// Something the character has with its origin: a damage type it is immune to, a condition it ignores, a saving
/// throw proficiency it lost... (<see cref="Source"/> is a <see cref="BreakdownSources"/> value).
/// </summary>
public sealed record SourcedValue(string Value, string Source, string Label);

/// <summary>A natural weapon (claws, bite...) the character has, with what grants it.</summary>
public sealed record NaturalWeaponValue(NaturalWeapon Weapon, string Label);

/// <summary>A movement other than walking ("swim", "climb", "fly") in feet; breakdown "speed.&lt;target&gt;".</summary>
public sealed record SpeedValue(string Mode, int Feet);

/// <summary>A damage type the character resists, with where it comes from (<see cref="Source"/> is a <see cref="BreakdownSources"/> value).</summary>
public sealed record ResistanceValue(string DamageType, string Source, string Label);

/// <summary>
/// Breath weapon of a draconic ancestry: damage dice at the character level, damage type, saving throw ability, area and
/// DC (8 + Constitution + proficiency, see the "breathWeapon.dc" breakdown).
/// </summary>
public sealed record BreathWeaponValue(string Name, string Source, string DamageType, string Dice, string SaveAbility, string Area, int Dc);

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

    /// <summary>
    /// Modifiers of the chosen options and feats (every one, conditional ones included); the attack and damage
    /// bonuses are applied by <see cref="CombatCalculator"/> to the attacks that meet their condition.
    /// </summary>
    public IReadOnlyList<FeatureModifier> FeatureModifiers { get; init; } = [];

    /// <summary>Automatic resources granted by the chosen options, for <see cref="Character.SyncAutoResources"/>.</summary>
    public IReadOnlyList<ResourceTemplate> ChoiceResources { get; init; } = [];

    /// <summary>Damage resistances of the race (dwarf: poison) and of the chosen trait options (draconic ancestry).</summary>
    public IReadOnlyList<ResistanceValue> Resistances { get; init; } = [];

    /// <summary>Breath weapon of the chosen draconic ancestry, or null.</summary>
    public BreathWeaponValue? BreathWeapon { get; init; }

    /// <summary>Whether the character wears armor (conditions such as Defense depend on it).</summary>
    public bool WearsArmor { get; init; }

    /// <summary>Damage types the character is immune to (content packs).</summary>
    public IReadOnlyList<SourcedValue> Immunities { get; init; } = [];

    /// <summary>Conditions the character is immune to (content packs).</summary>
    public IReadOnlyList<SourcedValue> ConditionImmunities { get; init; } = [];

    /// <summary>Saving throw proficiencies the character lost (origin feats), the ability in <see cref="SourcedValue.Value"/>.</summary>
    public IReadOnlyList<SourcedValue> LostSavingThrows { get; init; } = [];

    /// <summary>Save DCs of the classes besides the spell one (content packs).</summary>
    public IReadOnlyList<ClassSaveDcValue> ClassSaveDcs { get; init; } = [];

    /// <summary>Passive Investigation (breakdown "passiveInvestigation").</summary>
    public int PassiveInvestigation { get; init; }

    /// <summary>Passive Insight (breakdown "passiveInsight").</summary>
    public int PassiveInsight { get; init; }

    /// <summary>Highest score of each ability (20 unless a feature changes it).</summary>
    public IReadOnlyDictionary<string, int> AbilityMaxScores { get; init; } = new Dictionary<string, int>();

    /// <summary>Size of the character ("medium", lower case; empty without race), with what sets it in <see cref="SizeSource"/>.</summary>
    public string Size { get; init; } = string.Empty;

    /// <summary>The race, or the feature that changed the size.</summary>
    public string? SizeSource { get; init; }

    /// <summary>Swimming, climbing and flying speeds (only those the character has).</summary>
    public IReadOnlyList<SpeedValue> Speeds { get; init; } = [];

    /// <summary>Natural weapons granted by the race, options and feats; <see cref="CombatCalculator"/> makes them attacks.</summary>
    public IReadOnlyList<NaturalWeaponValue> NaturalWeapons { get; init; } = [];

    /// <summary>Failed death saving throws that kill the character (3 unless a feature changes it).</summary>
    public int DeathSaveFailuresToDie { get; init; } = SheetCalculator.DefaultDeathSaveFailures;

    /// <summary>What changed <see cref="DeathSaveFailuresToDie"/>, or null.</summary>
    public string? DeathSaveFailuresSource { get; init; }

    /// <summary>Whether a long rest recovers every hit die (instead of half).</summary>
    public bool RecoversAllHitDice { get; init; }

    /// <summary>What makes a long rest recover every hit die, or null.</summary>
    public string? HitDiceRecoverySource { get; init; }

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

    /// <summary>Classes that prepare spells and can already cast leveled spells (see <see cref="SpellcastingValue.PreparesSpells"/>).</summary>
    public IReadOnlyList<SpellcastingValue> PreparingClasses => [.. Spellcasting.Where(s => s.PreparesSpells)];

    public bool IsOverridden(string field) => OverriddenFields.Contains(field, StringComparer.Ordinal);
}
