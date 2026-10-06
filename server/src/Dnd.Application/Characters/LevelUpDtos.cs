using System.Text.Json;
using FluentValidation;

namespace Dnd.Application.Characters;

// Level-up wizard (phase 16c): GET /characters/{id}/level-up returns the plan for one class,
// POST /characters/{id}/level-up applies it with the hit points rolled and the answers to the choices.

/// <summary>
/// What gaining the next level in <see cref="ClassIndex"/> means: the classes the character could take
/// (multiclassing prerequisites included), the hit die to roll, the features gained automatically, the
/// choices to make and the spellcasting limits of the class at its new level.
/// </summary>
/// <param name="CurrentLevel">Total level now.</param>
/// <param name="TargetLevel">Total level after the level-up.</param>
/// <param name="ClassLevel">Level of <see cref="ClassIndex"/> after the level-up (1 for a new class).</param>
/// <param name="HitDie">Die to roll for the hit points (the player writes the result, 1..die).</param>
/// <param name="ConModifier">Constitution modifier added to the roll (minimum 1 hit point per level).</param>
public sealed record LevelUpPlanDto(
    Guid CharacterId,
    int CurrentLevel,
    int TargetLevel,
    string ClassIndex,
    int ClassLevel,
    int HitDie,
    int ConModifier,
    IReadOnlyList<LevelUpClassDto> Classes,
    IReadOnlyList<LevelUpFeatureDto> AutomaticFeatures,
    IReadOnlyList<LevelUpChoiceDto> Choices,
    LevelUpSpellcastingDto? Spellcasting);

/// <summary>A class the character could gain a level in.</summary>
/// <param name="Allowed">False when a new class does not meet the multiclassing prerequisites (see <see cref="Reason"/>).</param>
/// <param name="IsNew">True when the character does not have the class yet (multiclassing).</param>
/// <param name="CurrentLevel">Level in the class now (0 for a new class).</param>
public sealed record LevelUpClassDto(
    string ClassIndex,
    string Name,
    bool Allowed,
    string? Reason,
    int HitDie,
    bool IsNew,
    int CurrentLevel,
    string? SubclassIndex);

/// <summary>A feature gained automatically at the new level. <see cref="SubclassIndex"/> is set for subclass features.</summary>
public sealed record LevelUpFeatureDto(string ClassIndex, string? SubclassIndex, int Level, LevelUpFeatureInfoDto Feature);

public sealed record LevelUpFeatureInfoDto(string Index, string Name, IReadOnlyList<string> Description);

/// <summary>
/// A choice to make at the new level. <see cref="Required"/> picks are needed (<see cref="Choose"/>, or fewer when
/// not enough options are eligible). With <see cref="Replaces"/>, up to one of <see cref="Known"/> may be swapped:
/// it goes in <c>replaced</c> and one more pick is sent. <see cref="SubclassIndex"/>: the choice only applies with
/// that subclass (it is chosen at this same level). <see cref="FreeText"/>: no list, any text (languages, tools...).
/// </summary>
/// <param name="Kind">A <c>LevelChoiceKind</c> name (Subclass, OptionSet, AsiOrFeat, Expertise, Skill, Language, Tool, CantripsKnown, SpellsKnown, SpellbookSpells, Custom).</param>
public sealed record LevelUpChoiceDto(
    string Key,
    string Name,
    string Kind,
    int Choose,
    int Required,
    bool Replaces,
    bool Cumulative,
    string Note,
    string? SubclassIndex,
    string? SetId,
    bool FreeText,
    IReadOnlyList<LevelUpOptionDto> Options,
    IReadOnlyList<ChoiceItemDto> Known);

/// <summary>
/// An option of a choice. Not <see cref="Eligible"/> when its prerequisites are not met (<see cref="Reason"/>).
/// <see cref="SpellLevel"/> and <see cref="SpellCategory"/> (a <c>SpellCategory</c> name) for spells;
/// <see cref="AbilityIncrease"/> for feats that raise an ability.
/// </summary>
public sealed record LevelUpOptionDto(
    string Index,
    string Name,
    IReadOnlyList<string> Description,
    string? PrerequisitesText,
    bool Eligible,
    string? Reason,
    int? SpellLevel,
    IReadOnlyList<EffectPreviewDto> EffectsPreview,
    AbilityIncreaseDto? AbilityIncrease,
    string? SpellCategory = null);

/// <summary>
/// A numeric effect of an option, as a breakdown part (<see cref="Source"/> "feature", <see cref="Label"/> the value it
/// changes, e.g. "CA") plus the affected sheet field and its value before and after ("CA 16 → 17"). Conditional effects
/// (<see cref="Condition"/>, Spanish) only change <see cref="After"/> when the condition holds now.
/// </summary>
public sealed record EffectPreviewDto(string Source, string Label, int Value, string? Field, int? Before, int? After, string? Condition);

/// <summary>A feat raises one ability of <see cref="From"/> (empty: any) by <see cref="Amount"/>.</summary>
public sealed record AbilityIncreaseDto(int Amount, IReadOnlyList<string> From);

/// <summary>Spellcasting of the class at its new level.</summary>
/// <param name="CantripsKnown">Cantrips known at the new level (null when the class table has none).</param>
/// <param name="SpellsKnown">Spells known at the new level (classes that know spells; null otherwise).</param>
/// <param name="MaxSpellLevel">Highest spell level with slots of the class at the new level (0 without slots).</param>
/// <param name="CurrentCantrips">Cantrips the character has for the class now.</param>
/// <param name="CurrentSpells">Spells (level 1+) the character has for the class now (the spellbook for wizards).</param>
/// <param name="SpellSlots">Slots of the class table at the new level (9 entries).</param>
public sealed record LevelUpSpellcastingDto(
    string ClassIndex,
    string? Ability,
    bool IsPactCaster,
    int? CantripsKnown,
    int? SpellsKnown,
    int MaxSpellLevel,
    int CurrentCantrips,
    int CurrentSpells,
    IReadOnlyList<int> SpellSlots);

/// <summary>Body of <c>POST /characters/{id}/level-up</c>.</summary>
/// <param name="ClassIndex">Class that gains the level (a new one to multiclass); null = the main class.</param>
/// <param name="HitPointsRolled">Result of the hit die, 1..die; the server adds Constitution.</param>
public sealed record LevelUpRequest(string? ClassIndex, int HitPointsRolled, IReadOnlyList<LevelUpChoiceAnswer>? Choices);

/// <summary>
/// The answer to one choice of the plan. <see cref="Selected"/> is an array of indexes (or texts), or for
/// <c>AsiOrFeat</c> an object: <c>{ "asi": { "str": 1, "dex": 1 } }</c> or <c>{ "feat": "grappler", "ability": "str" }</c>.
/// <see cref="Replaced"/>: known picks swapped out (choices with <c>replaces</c>, at most one).
/// </summary>
public sealed record LevelUpChoiceAnswer(string? Key, JsonElement Selected, IReadOnlyList<string>? Replaced);

public sealed class LevelUpRequestValidator : AbstractValidator<LevelUpRequest>
{
    public const int MaxChoices = 50;

    public LevelUpRequestValidator()
    {
        RuleFor(x => x.HitPointsRolled).GreaterThanOrEqualTo(1).WithMessage("La tirada de puntos de golpe debe ser al menos 1.");
        RuleFor(x => x.ClassIndex).MaximumLength(100).WithMessage("El índice de clase no puede superar los 100 caracteres.");
        RuleFor(x => x.Choices!.Count).LessThanOrEqualTo(MaxChoices)
            .WithMessage($"No se admiten más de {MaxChoices} elecciones.")
            .When(x => x.Choices is not null)
            .OverridePropertyName("choices");
        RuleForEach(x => x.Choices).Must(c => c is not null && !string.IsNullOrWhiteSpace(c.Key))
            .WithMessage("Cada elección necesita su key.")
            .When(x => x.Choices is not null);
    }
}
