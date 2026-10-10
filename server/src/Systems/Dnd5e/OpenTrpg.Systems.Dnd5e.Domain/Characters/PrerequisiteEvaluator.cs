using OpenTrpg.Core.Domain.Characters;
using OpenTrpg.Systems.Dnd5e.Domain.Catalog;

namespace OpenTrpg.Systems.Dnd5e.Domain.Characters;

/// <summary>
/// What a prerequisite is checked against: the character and its sheet, plus what the request being planned adds
/// (picks, spells, subclasses and the class level gained). Names turn indexes into the texts of the reasons.
/// </summary>
public sealed record PrerequisiteFacts
{
    public required Dnd5eCharacter Character { get; init; }

    public required CharacterSheet Sheet { get; init; }

    /// <summary>Class of the choice: <see cref="OptionPrerequisites.MinLevel"/> counts its level (the total level when null).</summary>
    public string? ClassIndex { get; init; }

    /// <summary>Level in <see cref="ClassIndex"/> to compare with <see cref="OptionPrerequisites.MinLevel"/> (the new level when planning).</summary>
    public int ClassLevel { get; init; }

    /// <summary>Total level to compare with <see cref="OptionPrerequisites.CharacterLevel"/>.</summary>
    public int CharacterLevel { get; init; }

    /// <summary>Option indexes the character has or picks in the same request.</summary>
    public IReadOnlySet<string> Picks { get; init; } = new HashSet<string>(StringComparer.Ordinal);

    /// <summary>Spells and subclasses picked in the same request.</summary>
    public IReadOnlySet<string> Pending { get; init; } = new HashSet<string>(StringComparer.Ordinal);

    /// <summary>Score of an ability (the sheet's when null).</summary>
    public Func<string, int>? Score { get; init; }

    /// <summary>Size of the character's race ("Medium"), or null when unknown.</summary>
    public string? Size { get; init; }

    /// <summary>Whether a class casts with pact magic.</summary>
    public Func<string, bool> IsPactClass { get; init; } = classIndex => classIndex == "warlock";

    /// <summary>Display name of an option, spell, subclass, race or class index.</summary>
    public Func<string, string> Name { get; init; } = index => index;

    /// <summary>Name of the pact boon the character has, for the reasons ("tienes Pacto del filo").</summary>
    public string? CurrentPactBoon { get; init; }

    /// <summary>Add what the character has to the reasons ("Requiere Fuerza 13; tienes 12.").</summary>
    public bool Detailed { get; init; }

    public int ScoreOf(string ability) => Score?.Invoke(ability) ?? Sheet.Abilities[ability].Score;
}

/// <summary>
/// Evaluates the structured prerequisites of options, feats, subclasses and auxiliary classes (format 3 of the content
/// packs) and writes them in Spanish. Shared by the level-up planner, the invalid choices and the origin feats.
/// </summary>
public static class PrerequisiteEvaluator
{
    /// <summary>Why the prerequisites are not met (Spanish, one sentence per failure), or null when they are.</summary>
    public static string? WhyNot(OptionPrerequisites prerequisites, PrerequisiteFacts facts)
    {
        var reasons = Reasons(prerequisites, facts);
        return reasons.Count == 0 ? null : string.Join(" ", reasons);
    }

    public static IReadOnlyList<string> Reasons(OptionPrerequisites prerequisites, PrerequisiteFacts facts)
    {
        ArgumentNullException.ThrowIfNull(prerequisites);
        ArgumentNullException.ThrowIfNull(facts);
        var character = facts.Character;
        var reasons = new List<string>();
        var detailed = facts.Detailed;

        if (prerequisites.MinLevel is { } minLevel && facts.ClassLevel < minLevel)
        {
            reasons.Add(facts.ClassIndex is null
                ? $"Requiere nivel {minLevel}{(detailed ? $"; tienes {facts.ClassLevel}" : string.Empty)}."
                : $"Requiere nivel {minLevel} de {facts.Name(facts.ClassIndex)}{(detailed ? $"; subes al {facts.ClassLevel}" : string.Empty)}.");
        }

        if (prerequisites.CharacterLevel is { } characterLevel && facts.CharacterLevel < characterLevel)
        {
            reasons.Add($"Requiere nivel de personaje {characterLevel}{(detailed ? $"; tienes {facts.CharacterLevel}" : string.Empty)}.");
        }

        if (prerequisites.PactBoon is { } pactBoon && !facts.Picks.Contains(pactBoon))
        {
            reasons.Add(!detailed ? $"Requiere {facts.Name(pactBoon)}."
                : facts.CurrentPactBoon is null ? $"Requiere {facts.Name(pactBoon)}; aún no tienes don del pacto."
                : $"Requiere {facts.Name(pactBoon)}; tienes {facts.CurrentPactBoon}.");
        }

        if (prerequisites.Cantrip is { } cantrip && !Knows(facts, cantrip))
        {
            reasons.Add($"Requiere el truco {facts.Name(cantrip)}{(detailed ? "; no lo conoces" : string.Empty)}.");
        }

        foreach (var (ability, minimum) in prerequisites.Abilities)
        {
            if (Abilities.IsValid(ability) && facts.ScoreOf(ability) < minimum)
            {
                reasons.Add($"Requiere {BreakdownLabels.Ability(ability)} {minimum}{(detailed ? $"; tienes {facts.ScoreOf(ability)}" : string.Empty)}.");
            }
        }

        if (prerequisites.Races.Count > 0 && (character.RaceIndex is null || !prerequisites.Races.Contains(character.RaceIndex, StringComparer.Ordinal)))
        {
            var required = string.Join(" o ", prerequisites.Races.Select(facts.Name));
            reasons.Add(!detailed ? $"Requiere ser {required}."
                : character.RaceIndex is null ? $"Requiere ser {required}; no tienes raza."
                : $"Requiere ser {required}; eres {facts.Name(character.RaceIndex)}.");
        }

        // Without a known size (no race, or a check that does not load it) the size is not checked.
        if (prerequisites.Sizes.Count > 0 && facts.Size is { Length: > 0 } size && !prerequisites.Sizes.Contains(size.ToLowerInvariant(), StringComparer.Ordinal))
        {
            reasons.Add($"Requiere tamaño {string.Join(" o ", prerequisites.Sizes.Select(SizeName))}{(detailed ? $"; eres {SizeName(size.ToLowerInvariant())}" : string.Empty)}.");
        }

        var armorKeys = character.Proficiencies.Where(p => p.Type == ProficiencyType.Armor).Select(p => p.Key).ToList();
        foreach (var armor in prerequisites.ArmorProficiencies.Where(a => !ProficiencyKeys.HasArmor(armorKeys, a)))
        {
            reasons.Add($"Requiere competencia con {ProficiencyKeys.Describe(armor)}{(detailed ? "; no la tienes" : string.Empty)}.");
        }

        foreach (var weapon in prerequisites.WeaponProficiencies.Where(w => character.FindProficiency(ProficiencyType.Weapon, w) is null))
        {
            reasons.Add($"Requiere competencia con {ProficiencyKeys.Describe(weapon)}{(detailed ? "; no la tienes" : string.Empty)}.");
        }

        if (prerequisites.Spellcasting && character.Spells.Count == 0 && !facts.Sheet.Spellcasting.Any(c => c.MaxSpellLevel > 0))
        {
            reasons.Add(detailed ? "Requiere poder lanzar al menos un conjuro; no lanzas conjuros." : "Requiere poder lanzar al menos un conjuro.");
        }

        if (prerequisites.Subclasses.Count > 0 && !prerequisites.Subclasses.Any(s => HasSubclass(facts, s)))
        {
            reasons.Add($"Requiere la subclase {string.Join(" o ", prerequisites.Subclasses.Select(facts.Name))}.");
        }

        foreach (var option in prerequisites.Options.Where(o => !facts.Picks.Contains(o)))
        {
            reasons.Add($"Requiere {facts.Name(option)}{(detailed ? "; no lo tienes" : string.Empty)}.");
        }

        if ((prerequisites.ClassFeature is not null || prerequisites.Classes.Count > 0) && !HasClassFeature(prerequisites, facts))
        {
            reasons.Add($"Requiere {ClassFeatureText(prerequisites, facts.Name)}.");
        }

        if (prerequisites.SpellcastingAbilityMin is { } abilityMin)
        {
            var best = facts.Sheet.Spellcasting
                .Where(c => prerequisites.Classes.Count == 0 || prerequisites.Classes.Contains(c.ClassIndex, StringComparer.Ordinal))
                .Where(c => Abilities.IsValid(c.Ability))
                .Select(c => facts.ScoreOf(c.Ability))
                .DefaultIfEmpty(0)
                .Max();
            if (best < abilityMin)
            {
                reasons.Add($"Requiere {abilityMin} en tu característica de lanzamiento{(detailed ? best == 0 ? "; no lanzas conjuros" : $"; tienes {best}" : string.Empty)}.");
            }
        }

        if (prerequisites.AnyOf.Count > 0 && !prerequisites.AnyOf.Any(a => Reasons(a, facts).Count == 0))
        {
            reasons.Add($"Requiere una de estas opciones: {string.Join("; ", prerequisites.AnyOf.Select(a => Describe(a, facts.Name, facts.ClassIndex)))}.");
        }

        return reasons;
    }

    /// <summary>
    /// Spanish text of the prerequisites ("Fuerza 13, competencia con armadura pesada"), or null when there are none.
    /// <paramref name="classIndex"/> names the class of <see cref="OptionPrerequisites.MinLevel"/>.
    /// </summary>
    public static string? Text(OptionPrerequisites prerequisites, Func<string, string> name, string? classIndex = null)
    {
        ArgumentNullException.ThrowIfNull(prerequisites);
        if (prerequisites.IsEmpty)
        {
            return null;
        }

        var text = Describe(prerequisites, name, classIndex);
        return text.Length == 0 ? null : char.ToUpperInvariant(text[0]) + text[1..];
    }

    private static string Describe(OptionPrerequisites prerequisites, Func<string, string> name, string? classIndex)
    {
        var parts = new List<string>();
        if (prerequisites.MinLevel is { } minLevel)
        {
            parts.Add(classIndex is null ? $"nivel {minLevel}" : $"nivel {minLevel} de {name(classIndex)}");
        }

        if (prerequisites.CharacterLevel is { } characterLevel)
        {
            parts.Add($"nivel de personaje {characterLevel}");
        }

        if (prerequisites.PactBoon is { } pactBoon)
        {
            parts.Add(name(pactBoon));
        }

        if (prerequisites.Cantrip is { } cantrip)
        {
            parts.Add($"el truco {name(cantrip)}");
        }

        parts.AddRange(prerequisites.Abilities.Where(a => Abilities.IsValid(a.Key)).Select(a => $"{BreakdownLabels.Ability(a.Key)} {a.Value}"));
        if (prerequisites.Races.Count > 0)
        {
            parts.Add($"ser {string.Join(" o ", prerequisites.Races.Select(name))}");
        }

        if (prerequisites.Sizes.Count > 0)
        {
            parts.Add($"tamaño {string.Join(" o ", prerequisites.Sizes.Select(SizeName))}");
        }

        parts.AddRange(prerequisites.ArmorProficiencies.Select(a => $"competencia con {ProficiencyKeys.Describe(a)}"));
        parts.AddRange(prerequisites.WeaponProficiencies.Select(w => $"competencia con {ProficiencyKeys.Describe(w)}"));
        if (prerequisites.Spellcasting)
        {
            parts.Add("poder lanzar al menos un conjuro");
        }

        if (prerequisites.Subclasses.Count > 0)
        {
            parts.Add($"la subclase {string.Join(" o ", prerequisites.Subclasses.Select(name))}");
        }

        parts.AddRange(prerequisites.Options.Select(name));
        if (prerequisites.ClassFeature is not null || prerequisites.Classes.Count > 0)
        {
            parts.Add(ClassFeatureText(prerequisites, name));
        }

        if (prerequisites.SpellcastingAbilityMin is { } abilityMin)
        {
            parts.Add($"{abilityMin} en la característica de lanzamiento");
        }

        if (prerequisites.AnyOf.Count > 0)
        {
            parts.Add(string.Join(" o ", prerequisites.AnyOf.Select(a => $"({Describe(a, name, classIndex)})")));
        }

        return string.Join(", ", parts);
    }

    private static string ClassFeatureText(OptionPrerequisites prerequisites, Func<string, string> name)
    {
        var classes = prerequisites.Classes.Count == 0 ? null : string.Join(" o ", prerequisites.Classes.Select(name));
        return prerequisites.ClassFeature switch
        {
            ClassFeatures.Spellcasting => classes is null ? "el rasgo Lanzamiento de conjuros" : $"el rasgo Lanzamiento de conjuros de {classes}",
            ClassFeatures.PactMagic => classes is null ? "el rasgo Magia de pacto" : $"el rasgo Magia de pacto de {classes}",
            _ => $"un nivel de {classes}",
        };
    }

    private static bool Knows(PrerequisiteFacts facts, string spell) =>
        facts.Character.Spells.Any(s => s.SpellIndex == spell) || facts.Pending.Contains(spell);

    private static bool HasSubclass(PrerequisiteFacts facts, string subclass) =>
        facts.Character.Classes.Any(c => c.SubclassIndex == subclass) || facts.Pending.Contains(subclass);

    /// <summary>A class of the character (in <see cref="OptionPrerequisites.Classes"/> when given) with the required feature.</summary>
    private static bool HasClassFeature(OptionPrerequisites prerequisites, PrerequisiteFacts facts)
    {
        var classes = facts.Character.Classes.Select(c => c.ClassIndex)
            .Where(c => prerequisites.Classes.Count == 0 || prerequisites.Classes.Contains(c, StringComparer.Ordinal))
            .ToList();
        var casting = facts.Sheet.Spellcasting.Select(c => c.ClassIndex).ToHashSet(StringComparer.Ordinal);
        return prerequisites.ClassFeature switch
        {
            ClassFeatures.Spellcasting => classes.Any(c => casting.Contains(c) && !facts.IsPactClass(c)),
            ClassFeatures.PactMagic => classes.Any(c => casting.Contains(c) && facts.IsPactClass(c)),
            _ => classes.Count > 0,
        };
    }

    private static string SizeName(string size) => size switch
    {
        "tiny" => "Diminuto",
        "small" => "Pequeño",
        "medium" => "Mediano",
        "large" => "Grande",
        "huge" => "Enorme",
        "gargantuan" => "Gargantuesco",
        _ => size,
    };
}
