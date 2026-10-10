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

    /// <summary>"srd" or the id of the content pack that defines the class (format 3 <c>classes[]</c>).</summary>
    public string Source { get; init; } = Dnd5eCatalogSources.Srd;

    /// <summary>Description paragraphs (content packs; empty for the SRD classes).</summary>
    public IReadOnlyList<string> Description { get; init; } = [];

    /// <summary>Class level at which the subclass is chosen (content packs); 0 when the level choices say it.</summary>
    public int SubclassLevel { get; init; }

    /// <summary>
    /// Spellcasting details of a pack class as a JSON object (<see cref="ClassSpellcastingInfo"/>: progression,
    /// preparation, ritual casting and focus); null for the SRD classes and the classes that do not cast.
    /// </summary>
    public string? SpellcastingJson { get; init; }

    public ClassSpellcastingInfo? Spellcasting => ClassSpellcastingInfo.Parse(SpellcastingJson);

    /// <summary>Multiclassing of a pack class (<see cref="ClassMulticlassing"/>); null for the SRD classes (fixed tables).</summary>
    public string? MulticlassJson { get; init; }

    public ClassMulticlassing? Multiclassing => ClassMulticlassing.Parse(MulticlassJson);

    /// <summary>
    /// Limited-use resources of a pack class as a JSON array of resources (same shape as an option's, the
    /// <c>classSpecific:</c> maxima already turned into <c>byLevel</c> tables); null when it has none.
    /// </summary>
    public string? ResourcesJson { get; init; }

    public IReadOnlyList<OptionResource> Resources => LevelChoiceJson.ParseResources(ResourcesJson);

    /// <summary>
    /// Spell list of a pack class beyond the spells that name it: <c>{"spells": ["fireball"], "classes": ["wizard"]}</c>
    /// (spell indexes, and classes whose whole list it inherits); null when the spells themselves list it.
    /// </summary>
    public string? SpellListJson { get; init; }

    public ClassSpellList? SpellList => ClassSpellList.Parse(SpellListJson);

    /// <summary>Whether the class prepares its spells from its whole list (cleric, druid, paladin, wizard and pack classes with <c>"preparation": "prepared"</c>).</summary>
    public bool PreparesSpells => Index is "cleric" or "druid" or "paladin" or "wizard" || Spellcasting?.Preparation == ClassSpellcastingInfo.Prepared;
}
