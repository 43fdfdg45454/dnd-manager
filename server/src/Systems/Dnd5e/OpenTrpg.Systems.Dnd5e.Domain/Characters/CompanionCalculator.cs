using System.Globalization;
using System.Text.RegularExpressions;
using OpenTrpg.Core.Domain.Catalog;
using OpenTrpg.Core.Domain.Characters;
using OpenTrpg.Systems.Dnd5e.Domain.Catalog;
using OpenTrpg.Systems.Dnd5e.Domain.Characters;

namespace OpenTrpg.Systems.Dnd5e.Domain.Characters;

/// <summary>One damage component of a beast attack ("2d4+2" piercing).</summary>
public sealed record CompanionDamage(string Dice, string? Type);

/// <summary>An action of the beast as the catalog prints it; <see cref="AttackBonus"/> is null for actions without an attack roll.</summary>
public sealed record CompanionBeastAction(string Name, string Description, int? AttackBonus, IReadOnlyList<CompanionDamage> Damage, bool IsMultiattack);

/// <summary>What the calculation needs of a beast of the catalog.</summary>
/// <param name="SavingThrows">Bonuses of the saving throws the beast is proficient in, by ability index.</param>
/// <param name="Skills">Bonuses of the skills the beast is proficient in, by skill index.</param>
public sealed record CompanionBeast(
    string Index,
    string Name,
    string Size,
    double ChallengeRating,
    int ArmorClass,
    int HitPoints,
    IReadOnlyDictionary<string, int> SavingThrows,
    IReadOnlyDictionary<string, int> Skills,
    IReadOnlyList<CompanionBeastAction> Actions);

/// <summary>
/// An action of the companion: the attack bonus and the damage with the character's proficiency bonus when the rule adds
/// it. <see cref="Damage"/> holds the dice to roll (the bonus folded into the first component); <see cref="DamageBonus"/>
/// explains the flat bonus of that first component.
/// </summary>
public sealed record CompanionAttack(
    string Name,
    string Description,
    ValueBreakdown? AttackBonus,
    IReadOnlyList<CompanionDamage> Damage,
    ValueBreakdown? DamageBonus,
    bool IsMultiattack);

/// <summary>The recalculated statblock of a companion; every value carries its breakdown.</summary>
public sealed record CompanionStatBlock(
    ValueBreakdown ArmorClass,
    ValueBreakdown HitPointsMax,
    IReadOnlyDictionary<string, ValueBreakdown> SavingThrows,
    IReadOnlyDictionary<string, ValueBreakdown> Skills,
    IReadOnlyList<CompanionAttack> Attacks);

/// <summary>
/// Statblock of an animal companion (phase 25, block 6), after the beast's own: the character's proficiency bonus is added to
/// the AC and to the saving throws and skills the beast is proficient in (<see cref="CompanionRule.ProficiencyBonusFromCharacter"/>),
/// and to its attack and damage rolls (<see cref="CompanionRule.AttackBonusFromCharacter"/>); the maximum hit points are the
/// beast's or N × the level of the feature's class, whichever is higher (<see cref="CompanionRule.HitPointsPerClassLevel"/>).
/// </summary>
public static partial class CompanionCalculator
{
    public const string BeastArmorClassLabel = "CA de la bestia";
    public const string BeastHitPointsLabel = "PG de la bestia";
    public const string BeastBonusLabel = "Bonificador de la bestia";
    public const string BeastAttackLabel = "Ataque de la bestia";
    public const string BeastDamageLabel = "Daño de la bestia";
    public const string CharacterProficiencyLabel = "Competencia del personaje";

    /// <param name="classIndex">Class of the feature that grants the companion.</param>
    /// <param name="classLevel">The character's level in that class.</param>
    /// <param name="proficiencyBonus">The character's proficiency bonus.</param>
    /// <param name="hitPointsMaxOverride">Maximum hit points set by hand, if any.</param>
    public static CompanionStatBlock Calculate(
        CompanionBeast beast,
        CompanionRule rule,
        string classIndex,
        int classLevel,
        int proficiencyBonus,
        int? hitPointsMaxOverride = null)
    {
        ArgumentNullException.ThrowIfNull(beast);
        ArgumentNullException.ThrowIfNull(rule);

        var addProficiency = rule.ProficiencyBonusFromCharacter;
        var armor = new BreakdownBuilder().Add(BreakdownSources.Base, BeastArmorClassLabel, beast.ArmorClass);
        if (addProficiency)
        {
            armor.Add(BreakdownSources.Proficiency, CharacterProficiencyLabel, proficiencyBonus);
        }

        var hitPoints = new BreakdownBuilder().Add(BreakdownSources.Base, BeastHitPointsLabel, beast.HitPoints);
        if (rule.HitPointsPerClassLevel is { } perLevel && perLevel * classLevel > beast.HitPoints)
        {
            hitPoints.SetTo(BreakdownSources.Class, $"{perLevel} × Nivel de {BreakdownLabels.Class(classIndex).ToLowerInvariant()} ({classLevel})", perLevel * classLevel);
        }

        if (hitPointsMaxOverride is { } maxOverride)
        {
            hitPoints.SetTo(BreakdownSources.Override, BreakdownLabels.ManualAdjustment, maxOverride);
        }

        return new CompanionStatBlock(
            armor.Build(),
            hitPoints.Build(),
            Proficient(beast.SavingThrows, addProficiency, proficiencyBonus),
            Proficient(beast.Skills, addProficiency, proficiencyBonus),
            beast.Actions.Select(a => Attack(a, rule.AttackBonusFromCharacter, proficiencyBonus)).ToList());
    }

    private static Dictionary<string, ValueBreakdown> Proficient(IReadOnlyDictionary<string, int> values, bool addProficiency, int proficiencyBonus) =>
        values.ToDictionary(
            v => v.Key,
            v =>
            {
                var builder = new BreakdownBuilder().Add(BreakdownSources.Base, BeastBonusLabel, v.Value);
                if (addProficiency)
                {
                    builder.Add(BreakdownSources.Proficiency, CharacterProficiencyLabel, proficiencyBonus);
                }

                return builder.Build();
            },
            StringComparer.Ordinal);

    private static CompanionAttack Attack(CompanionBeastAction action, bool addProficiency, int proficiencyBonus)
    {
        if (action.AttackBonus is not { } bonus)
        {
            return new CompanionAttack(action.Name, action.Description, null, action.Damage, null, action.IsMultiattack);
        }

        var attack = new BreakdownBuilder().Add(BreakdownSources.Base, BeastAttackLabel, bonus);
        if (addProficiency)
        {
            attack.Add(BreakdownSources.Proficiency, CharacterProficiencyLabel, proficiencyBonus);
        }

        if (action.Damage.Count == 0)
        {
            return new CompanionAttack(action.Name, action.Description, attack.Build(), [], null, action.IsMultiattack);
        }

        // The bonus goes once per attack, on the first damage component.
        var (dice, flat) = SplitDice(action.Damage[0].Dice);
        var damageBonus = new BreakdownBuilder().Add(BreakdownSources.Base, BeastDamageLabel, flat);
        if (addProficiency)
        {
            damageBonus.Add(BreakdownSources.Proficiency, CharacterProficiencyLabel, proficiencyBonus);
        }

        var damage = action.Damage.ToList();
        damage[0] = damage[0] with { Dice = JoinDice(dice, damageBonus.Total) };
        return new CompanionAttack(action.Name, action.Description, attack.Build(), damage, damageBonus.Build(), action.IsMultiattack);
    }

    /// <summary>"2d4+2" → ("2d4", 2); "1d6" → ("1d6", 0); "1d10-1" → ("1d10", -1). Anything else keeps its text with 0.</summary>
    public static (string Dice, int Flat) SplitDice(string dice)
    {
        var compact = dice.Replace(" ", string.Empty, StringComparison.Ordinal);
        var match = DicePattern().Match(compact);
        if (!match.Success)
        {
            return (compact, 0);
        }

        var flat = match.Groups[2].Success ? int.Parse(match.Groups[2].Value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture) : 0;
        return (match.Groups[1].Value, flat);
    }

    private static string JoinDice(string dice, int flat) => flat switch
    {
        > 0 => $"{dice}+{flat}",
        < 0 => $"{dice}{flat}",
        _ => dice,
    };

    [GeneratedRegex(@"^(\d*d\d+)([+-]\d+)?$", RegexOptions.CultureInvariant)]
    private static partial Regex DicePattern();
}

/// <summary>The companion feature a character has reached, with its rule and the level of its class.</summary>
public sealed record CompanionGrant(FeatureDefinition Feature, CompanionRule Rule, int ClassLevel)
{
    public string ClassIndex => Feature.ClassIndex;
}

public static class CompanionGrants
{
    /// <summary>
    /// The companion feature of one of the character's subclasses whose level the character has reached in that class
    /// (the highest one when several apply); null when none.
    /// </summary>
    public static CompanionGrant? Find(Dnd5eCharacter character, IEnumerable<FeatureDefinition> features)
    {
        ArgumentNullException.ThrowIfNull(character);
        ArgumentNullException.ThrowIfNull(features);
        return features
            .Where(f => f.SubclassIndex is not null)
            .Select(f => (Feature: f, Rule: f.Companion, Owner: character.Classes.FirstOrDefault(c => c.ClassIndex == f.ClassIndex && c.SubclassIndex == f.SubclassIndex)))
            .Where(f => f.Rule is not null && f.Owner is not null && f.Feature.Level <= f.Owner.Level)
            .OrderByDescending(f => f.Feature.Level)
            .ThenBy(f => f.Feature.Index, StringComparer.Ordinal)
            .Select(f => new CompanionGrant(f.Feature, f.Rule!, f.Owner!.Level))
            .FirstOrDefault();
    }
}
