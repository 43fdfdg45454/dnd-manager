using OpenTrpg.Core.Domain.Rules;

namespace OpenTrpg.Core.Domain.Characters;

/// <summary>The six ability scores of a character (base scores, without racial bonuses).</summary>
public sealed record AbilityScores(int Str, int Dex, int Con, int Int, int Wis, int Cha)
{
    /// <summary>All scores at 10, the default of a new character.</summary>
    public static AbilityScores Default { get; } = new(10, 10, 10, 10, 10, 10);

    /// <summary>Score by ability index ("str", "dex", ...).</summary>
    public int this[string ability] => ability switch
    {
        Abilities.Str => Str,
        Abilities.Dex => Dex,
        Abilities.Con => Con,
        Abilities.Int => Int,
        Abilities.Wis => Wis,
        Abilities.Cha => Cha,
        _ => throw new ArgumentOutOfRangeException(nameof(ability), ability, "Unknown ability index."),
    };

    internal void Validate()
    {
        foreach (var ability in Abilities.All)
        {
            if (this[ability] is < AbilityRules.MinScore or > AbilityRules.MaxScore)
            {
                throw Common.DomainException.RuleViolation(
                    $"Las puntuaciones de característica deben estar entre {AbilityRules.MinScore} y {AbilityRules.MaxScore}.");
            }
        }
    }
}
