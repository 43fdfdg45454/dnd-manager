using System.Text.RegularExpressions;
using OpenTrpg.Core.Domain.Common;
using OpenTrpg.Core.Domain.Rules;
using OpenTrpg.Core.Domain.Characters;
using OpenTrpg.Systems.Dnd5e.Domain.Characters;
using OpenTrpg.Systems.Dnd5e.Domain.Rules;

namespace OpenTrpg.Systems.Dnd5e.Domain.Characters;

/// <summary>
/// Sheet values that a <see cref="CharacterOverride"/> can replace: the fixed fields below plus
/// <c>ability.&lt;ability&gt;</c> (final score), <c>save.&lt;ability&gt;</c>, <c>skill.&lt;skillIndex&gt;</c> and
/// <c>spellcastingAbility.&lt;entry&gt;</c> (the ability of a spellcasting entry: a class index, <c>race</c> or the index of an
/// option that grants spells; the value is the position of the ability in <see cref="Abilities.All"/>, 1 = str … 6 = cha).
/// </summary>
public static partial class OverrideFields
{
    public const string HitPointsMax = "hitPointsMax";
    public const string ArmorClass = "armorClass";
    public const string Speed = "speed";
    public const string Initiative = "initiative";
    public const string ProficiencyBonus = "proficiencyBonus";
    public const string PassivePerception = "passivePerception";
    public const string SpellSaveDc = "spellSaveDc";
    public const string SpellAttackBonus = "spellAttackBonus";

    public const string AbilityPrefix = "ability.";
    public const string SavePrefix = "save.";
    public const string SkillPrefix = "skill.";
    public const string SpellcastingAbilityPrefix = "spellcastingAbility.";

    /// <summary>Lowest and highest value accepted by any override other than ability scores.</summary>
    public const int MinValue = -99;
    public const int MaxValue = 9999;

    /// <summary>Fields without parameter.</summary>
    public static IReadOnlyList<string> Fixed { get; } =
        [HitPointsMax, ArmorClass, Speed, Initiative, ProficiencyBonus, PassivePerception, SpellSaveDc, SpellAttackBonus];

    public static string Ability(string ability) => AbilityPrefix + ability;

    public static string Save(string ability) => SavePrefix + ability;

    public static string Skill(string skillIndex) => SkillPrefix + skillIndex;

    public static string SpellcastingAbility(string entry) => SpellcastingAbilityPrefix + entry;

    /// <summary>The ability of a <c>spellcastingAbility.*</c> override value (1 = str … 6 = cha), or null when out of range.</summary>
    public static string? AbilityOf(int value) => value is >= 1 and <= 6 ? Abilities.All[value - 1] : null;

    /// <summary>The <c>spellcastingAbility.*</c> override value of an ability (1 = str … 6 = cha).</summary>
    public static int ValueOf(string ability) => Abilities.All.ToList().IndexOf(ability) + 1;

    /// <summary>True when the field is one of the admitted override fields (skill indexes are checked for format only).</summary>
    public static bool IsValid(string field)
    {
        if (Fixed.Contains(field, StringComparer.Ordinal))
        {
            return true;
        }

        if (field.StartsWith(AbilityPrefix, StringComparison.Ordinal))
        {
            return Abilities.IsValid(field[AbilityPrefix.Length..]);
        }

        if (field.StartsWith(SavePrefix, StringComparison.Ordinal))
        {
            return Abilities.IsValid(field[SavePrefix.Length..]);
        }

        if (field.StartsWith(SpellcastingAbilityPrefix, StringComparison.Ordinal))
        {
            var entry = field[SpellcastingAbilityPrefix.Length..];
            return entry.Length <= Dnd5eCharacter.IndexMaxLength && SlugPattern().IsMatch(entry);
        }

        return field.StartsWith(SkillPrefix, StringComparison.Ordinal) && SlugPattern().IsMatch(field[SkillPrefix.Length..]);
    }

    /// <summary>Throws a <see cref="DomainException"/> when the field is unknown or the value out of range.</summary>
    internal static void Validate(string field, int value)
    {
        if (!IsValid(field))
        {
            throw DomainException.RuleViolation($"El campo '{field}' no admite sobrescritura.");
        }

        var (min, max) = field switch
        {
            _ when field.StartsWith(AbilityPrefix, StringComparison.Ordinal) => (AbilityRules.MinScore, AbilityRules.MaxScore),
            _ when field.StartsWith(SpellcastingAbilityPrefix, StringComparison.Ordinal) => (1, Abilities.All.Count),
            HitPointsMax or ArmorClass or Speed or PassivePerception or SpellSaveDc => (0, MaxValue),
            _ => (MinValue, MaxValue),
        };

        if (value < min || value > max)
        {
            throw DomainException.RuleViolation($"El valor de '{field}' debe estar entre {min} y {max}.");
        }
    }

    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$")]
    private static partial Regex SlugPattern();
}
