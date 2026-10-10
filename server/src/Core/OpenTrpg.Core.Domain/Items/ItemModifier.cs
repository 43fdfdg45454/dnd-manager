using OpenTrpg.Core.Domain.Characters;
using OpenTrpg.Core.Domain.Common;

namespace OpenTrpg.Core.Domain.Items;

/// <summary>What an <see cref="ItemModifier"/> changes in the sheet or the combat view.</summary>
public enum ItemModifierKind
{
    /// <summary>Adds <see cref="ItemModifier.Value"/> to the ability score named by the target ("str".."cha").</summary>
    AbilityBonus,

    /// <summary>Sets the ability score named by the target to the value, only when that is higher than the calculated score.</summary>
    AbilitySet,

    /// <summary>Adds the value to the saving throw of the target ability, or to every saving throw when the target is null.</summary>
    SaveBonus,

    /// <summary>Adds the value to the skill named by the target (skill index), or to every skill when the target is null.</summary>
    SkillBonus,

    /// <summary>Adds the value to the armor class (with or without armor).</summary>
    ArmorClassBonus,

    /// <summary>Weapons: attack bonus of that weapon only. Other items: bonus to every attack.</summary>
    AttackBonus,

    /// <summary>Weapons: damage bonus of that weapon only. Other items: bonus to the damage of every attack.</summary>
    DamageBonus,

    /// <summary>Adds the value (feet) to the speed.</summary>
    SpeedBonus,

    /// <summary>Adds the value to the maximum hit points.</summary>
    HitPointsMaxBonus,

    /// <summary>Adds the value to the initiative.</summary>
    InitiativeBonus,
}

/// <summary>
/// A structured effect of an item on the sheet or the combat view, applied while the item is active
/// (<see cref="EffectiveItem.IsActiveFor(CharacterItem)"/>). <see cref="Target"/> depends on
/// <see cref="Kind"/>: an ability index for <see cref="ItemModifierKind.AbilityBonus"/> and
/// <see cref="ItemModifierKind.AbilitySet"/> (required) and <see cref="ItemModifierKind.SaveBonus"/>
/// (null = all), a skill index for <see cref="ItemModifierKind.SkillBonus"/> (null = all), and null
/// for the other kinds.
/// </summary>
public sealed record ItemModifier(ItemModifierKind Kind, string? Target, int Value)
{
    /// <summary>
    /// The first problem of the modifier as a user-facing message, or null when it is valid. The target
    /// is checked as <see cref="Normalize"/> would leave it (trimmed, lower case, blank = null).
    /// </summary>
    public string? Validate()
    {
        if (!Enum.IsDefined(Kind))
        {
            return "El tipo de modificador no es válido.";
        }

        var target = NormalizeTarget(Target);
        switch (Kind)
        {
            case ItemModifierKind.AbilityBonus or ItemModifierKind.AbilitySet:
                if (target is null || !Abilities.IsValid(target))
                {
                    return "El modificador necesita una característica: str, dex, con, int, wis o cha.";
                }

                break;
            case ItemModifierKind.SaveBonus:
                if (target is not null && !Abilities.IsValid(target))
                {
                    return "La salvación del modificador debe ser str, dex, con, int, wis, cha o ninguna (todas).";
                }

                break;
            case ItemModifierKind.SkillBonus:
                if (target is { Length: > ItemLimits.ModifierTargetMaxLength })
                {
                    return $"La habilidad del modificador no puede superar los {ItemLimits.ModifierTargetMaxLength} caracteres.";
                }

                break;
            default:
                if (target is not null)
                {
                    return "Este tipo de modificador no admite objetivo.";
                }

                break;
        }

        var (min, max) = Kind == ItemModifierKind.AbilitySet
            ? (ItemLimits.MinAbilitySet, ItemLimits.MaxModifier)
            : (ItemLimits.MinModifier, ItemLimits.MaxModifier);
        return Value < min || Value > max ? $"El valor del modificador debe estar entre {min} y {max}." : null;
    }

    /// <summary>Validated copy with the target trimmed and in lower case (blank = null). Throws a <see cref="DomainException"/> when invalid.</summary>
    public ItemModifier Normalize()
    {
        if (Validate() is { } error)
        {
            throw DomainException.RuleViolation(error);
        }

        return this with { Target = NormalizeTarget(Target) };
    }

    /// <summary>
    /// Validated copy of a list of modifiers (each one normalized, at most <see cref="ItemLimits.MaxModifiers"/>).
    /// Null stays null; an empty list stays empty.
    /// </summary>
    public static IReadOnlyList<ItemModifier>? NormalizeAll(IReadOnlyList<ItemModifier>? modifiers)
    {
        if (modifiers is null)
        {
            return null;
        }

        if (modifiers.Count > ItemLimits.MaxModifiers)
        {
            throw DomainException.RuleViolation(TooManyMessage);
        }

        return modifiers.Select(m => m.Normalize()).ToArray();
    }

    /// <summary>Message for a list longer than <see cref="ItemLimits.MaxModifiers"/>.</summary>
    public static string TooManyMessage => $"Un objeto admite como máximo {ItemLimits.MaxModifiers} modificadores.";

    private static string? NormalizeTarget(string? target) =>
        string.IsNullOrWhiteSpace(target) ? null : target.Trim().ToLowerInvariant();
}
