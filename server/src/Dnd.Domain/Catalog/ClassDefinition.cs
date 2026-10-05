namespace Dnd.Domain.Catalog;

/// <summary>Character class from the rules catalog (SRD). <see cref="Index"/> is the primary key.</summary>
public sealed class ClassDefinition
{
    public required string Index { get; init; }

    public required string Name { get; init; }

    public int HitDie { get; init; }

    /// <summary>Ability indexes ("str", "dex", ...) of the saving throw proficiencies.</summary>
    public IReadOnlyList<string> SavingThrows { get; init; } = [];

    /// <summary>Armor, weapon and tool proficiencies (saving throws are in <see cref="SavingThrows"/>).</summary>
    public IReadOnlyList<string> ProficiencyNames { get; init; } = [];

    /// <summary>"int", "wis" or "cha"; null when the class does not cast spells.</summary>
    public string? SpellcastingAbility { get; init; }

    public bool IsSpellcaster { get; init; }

    /// <summary>
    /// Contribution to the multiclass spellcaster level: 1 = full caster, 2 = half caster,
    /// 3 = third caster, 0 = none. Warlocks are 1 but use pact magic (<see cref="IsPactCaster"/>).
    /// </summary>
    public int SpellcastingLevel { get; init; }

    /// <summary>True for Pact Magic (warlock): its slots do not merge with the multiclass table.</summary>
    public bool IsPactCaster { get; init; }

    /// <summary>Name of the subclass choice, e.g. "Primal Path".</summary>
    public string SubclassFlavor { get; init; } = string.Empty;

    /// <summary>One line per fixed item or choice of the starting equipment.</summary>
    public string StartingEquipmentText { get; init; } = string.Empty;
}
