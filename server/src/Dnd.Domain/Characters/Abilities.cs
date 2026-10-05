namespace Dnd.Domain.Characters;

/// <summary>Ability indexes as used by the SRD dataset and the override fields.</summary>
public static class Abilities
{
    public const string Str = "str";
    public const string Dex = "dex";
    public const string Con = "con";
    public const string Int = "int";
    public const string Wis = "wis";
    public const string Cha = "cha";

    /// <summary>The six abilities in sheet order.</summary>
    public static IReadOnlyList<string> All { get; } = [Str, Dex, Con, Int, Wis, Cha];

    public static bool IsValid(string ability) => All.Contains(ability, StringComparer.Ordinal);
}
