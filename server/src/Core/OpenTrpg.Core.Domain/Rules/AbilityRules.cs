namespace OpenTrpg.Core.Domain.Rules;

/// <summary>Core 5e arithmetic shared by every character sheet calculation.</summary>
public static class AbilityRules
{
    public const int MinScore = 1;
    public const int MaxScore = 30;
    public const int MinLevel = 1;
    public const int MaxLevel = 20;

    /// <summary>Ability modifier: floor((score - 10) / 2).</summary>
    public static int Modifier(int score)
    {
        if (score is < MinScore or > MaxScore)
        {
            throw new ArgumentOutOfRangeException(nameof(score), score, $"Ability scores must be between {MinScore} and {MaxScore}.");
        }

        return (int)Math.Floor((score - 10) / 2.0);
    }

    /// <summary>Proficiency bonus by total character level (+2 at 1st, +6 at 17th).</summary>
    public static int ProficiencyBonus(int characterLevel)
    {
        if (characterLevel is < MinLevel or > MaxLevel)
        {
            throw new ArgumentOutOfRangeException(nameof(characterLevel), characterLevel, $"Character level must be between {MinLevel} and {MaxLevel}.");
        }

        return 2 + (characterLevel - 1) / 4;
    }
}
