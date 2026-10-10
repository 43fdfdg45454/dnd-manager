using OpenTrpg.Core.Domain.Catalog;
using OpenTrpg.Systems.Dnd5e.Domain.Catalog;
namespace OpenTrpg.Systems.Dnd5e.Domain.Catalog;

/// <summary>Dnd5eCharacter class from the rules catalog (SRD). <see cref="Index"/> is the primary key.</summary>
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

    /// <summary>Skill proficiency choice at level 1: <c>{"choose":2,"from":["arcana","history"]}</c> (skill indexes without the "skill-" prefix).</summary>
    public string SkillChoicesJson { get; init; } = "{\"choose\":0,\"from\":[]}";

    /// <summary>One line per fixed item or choice of the starting equipment.</summary>
    public string StartingEquipmentText { get; init; } = string.Empty;

    /// <summary>Structured starting equipment (see <see cref="Catalog.StartingEquipment"/>), or null when unknown.</summary>
    public string? StartingEquipmentJson { get; init; }

    public StartingEquipment? StartingEquipment => Catalog.StartingEquipment.Parse(StartingEquipmentJson);
}
