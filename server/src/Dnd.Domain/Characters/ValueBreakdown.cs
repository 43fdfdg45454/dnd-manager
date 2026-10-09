namespace Dnd.Domain.Characters;

/// <summary>
/// One term of a calculated value. <see cref="Source"/> is one of <see cref="BreakdownSources"/>;
/// <see cref="Label"/> is user-facing text (Spanish), e.g. "Destreza" or the name of an item.
/// </summary>
public sealed record BreakdownPart(string Source, string Label, int Value);

/// <summary>
/// A calculated value explained point by point: the values of <see cref="Parts"/> always add up to
/// <see cref="Total"/>. An override is a final part worth (override − calculated), an ability set by
/// an item a part worth (set − score so far).
/// </summary>
public sealed record ValueBreakdown(int Total, IReadOnlyList<BreakdownPart> Parts);

/// <summary>Values of <see cref="BreakdownPart.Source"/>.</summary>
public static class BreakdownSources
{
    public const string Base = "base";
    public const string Race = "race";
    public const string Subrace = "subrace";
    public const string Background = "background";
    public const string Ability = "ability";
    public const string Proficiency = "proficiency";
    public const string Expertise = "expertise";
    public const string Class = "class";
    public const string Armor = "armor";
    public const string Shield = "shield";
    public const string Item = "item";
    public const string Override = "override";
    public const string Feature = "feature";
}

/// <summary>User-facing (Spanish) labels of the breakdown parts.</summary>
public static class BreakdownLabels
{
    public const string BaseScore = "Puntuación base";
    public const string Race = "Raza";
    public const string Subrace = "Subraza";
    public const string Chosen = "elección";
    public const string Background = "Trasfondo";
    public const string Proficiency = "Competencia";
    public const string Expertise = "Pericia";
    public const string Base = "Base";
    public const string Unarmored = "Sin armadura";
    public const string Armor = "Armadura";
    public const string Shield = "Escudo";
    public const string BaseSpeed = "Velocidad base";
    public const string ScoreLimit = "Límite de puntuación (1-30)";
    public const string Minimum = "Mínimo 0";
    public const string ManualAdjustment = "Ajuste manual";
    public const string ImprovementLimit = "Límite de las mejoras (20)";
    public const string ClassLevel = "Nivel de clase";
    public const string HalfClassLevel = "Mitad del nivel de clase";
    public const string UsesLimit = "Límite de usos (999)";

    /// <summary>"Mínimo 1": part that raises a resource maximum to its minimum.</summary>
    public static string MinimumOf(int minimum) => $"Mínimo {minimum}";

    /// <summary>"Ventaja táctica (nivel 3), tabla desde el nivel 7": entry of a by-level table.</summary>
    public static string ByLevel(string label, int level) => $"{label}, tabla desde el nivel {level}";

    private static readonly Dictionary<string, string> AbilityNames = new(StringComparer.Ordinal)
    {
        [Abilities.Str] = "Fuerza",
        [Abilities.Dex] = "Destreza",
        [Abilities.Con] = "Constitución",
        [Abilities.Int] = "Inteligencia",
        [Abilities.Wis] = "Sabiduría",
        [Abilities.Cha] = "Carisma",
    };

    private static readonly Dictionary<string, string> ClassNames = new(StringComparer.Ordinal)
    {
        ["barbarian"] = "Bárbaro",
        ["bard"] = "Bardo",
        ["cleric"] = "Clérigo",
        ["druid"] = "Druida",
        ["fighter"] = "Guerrero",
        ["monk"] = "Monje",
        ["paladin"] = "Paladín",
        ["ranger"] = "Explorador",
        ["rogue"] = "Pícaro",
        ["sorcerer"] = "Hechicero",
        ["warlock"] = "Brujo",
        ["wizard"] = "Mago",
    };

    /// <summary>"dex" → "Destreza"; unknown indexes are returned as they are.</summary>
    public static string Ability(string ability) => AbilityNames.GetValueOrDefault(ability, ability);

    /// <summary>"fighter" → "Guerrero"; unknown indexes are returned as they are.</summary>
    public static string Class(string classIndex) => ClassNames.GetValueOrDefault(classIndex, classIndex);

    /// <summary>"Ajuste manual", followed by the override note when there is one.</summary>
    public static string Override(string? note) =>
        string.IsNullOrWhiteSpace(note) ? ManualAdjustment : $"{ManualAdjustment}: {note.Trim()}";

    public static string Level(int level) => $"Nivel {level}";

    public static string UnarmoredDefense(string ability) => $"Defensa sin armadura ({Ability(ability)})";
}

/// <summary>Accumulates the parts of a <see cref="ValueBreakdown"/>, keeping their running total.</summary>
internal sealed class BreakdownBuilder
{
    private readonly List<BreakdownPart> _parts = [];

    public int Total { get; private set; }

    public BreakdownBuilder Add(string source, string label, int value)
    {
        _parts.Add(new BreakdownPart(source, label, value));
        Total += value;
        return this;
    }

    public BreakdownBuilder AddAll(IEnumerable<BreakdownPart> parts)
    {
        foreach (var part in parts)
        {
            Add(part.Source, part.Label, part.Value);
        }

        return this;
    }

    /// <summary>Adds the part that takes the total to <paramref name="value"/> (worth value − total, possibly 0).</summary>
    public BreakdownBuilder SetTo(string source, string label, int value) => Add(source, label, value - Total);

    /// <summary>Adds a <see cref="BreakdownLabels.ScoreLimit"/>-like part only when the total is outside [min, max].</summary>
    public BreakdownBuilder Clamp(int min, int max, string label)
    {
        var clamped = Math.Clamp(Total, min, max);
        return clamped == Total ? this : SetTo(BreakdownSources.Base, label, clamped);
    }

    public ValueBreakdown Build() => new(Total, _parts.ToArray());
}
