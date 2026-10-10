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

    /// <summary>
    /// Auxiliary level (content packs): a one-level class taken instead of a class level after level 1, once, without
    /// the ability prerequisites of multiclassing but with its own <see cref="Prerequisites"/>. Never a starting class.
    /// </summary>
    public bool IsAuxiliary { get; init; }

    /// <summary>Structured prerequisites of an auxiliary class (<see cref="OptionPrerequisites"/> JSON), or null.</summary>
    public string? PrerequisitesJson { get; init; }

    public OptionPrerequisites Prerequisites => LevelChoiceJson.ParsePrerequisites(PrerequisitesJson);

    public const int PrerequisitesTextMaxLength = 2000;

    /// <summary>Literal (or narrative) prerequisites of an auxiliary class, shown as they are.</summary>
    public string? PrerequisitesText { get; init; }

    /// <summary>
    /// An auxiliary class with spellcasting: counts as one more level of the character's caster class for its spell
    /// slots (not for spells known or prepared).
    /// </summary>
    public bool CountsAsCasterLevel { get; init; }

    /// <summary>
    /// A subclass may give the class spellcasting (<see cref="SubclassDefinition.Spellcasting"/>): the class adds nothing
    /// to the multiclass spellcaster level, has no Pact Magic and no table of its own (it does not cast, or only knows
    /// cantrips; the importer checks that its levels have no slots).
    /// </summary>
    public bool MayGainSubclassSpellcasting =>
        SpellcastingLevel == 0 && !IsPactCaster && Spellcasting?.Progression is null or ClassSpellcastingInfo.HitPoints or ClassSpellcastingInfo.RitualsOnly;

    /// <summary>
    /// Class save DCs of a pack class (8 + proficiency + ability) as a JSON array of <c>{"name", "ability"}</c>
    /// (<see cref="ClassSaveDc"/>); null when it has none.
    /// </summary>
    public string? SaveDcsJson { get; init; }

    public IReadOnlyList<ClassSaveDc> SaveDcs => ClassSaveDc.ParseAll(SaveDcsJson);

    /// <summary>Whether the class prepares its spells from its whole list (cleric, druid, paladin, wizard and pack classes with <c>"preparation": "prepared"</c>).</summary>
    public bool PreparesSpells => Index is "cleric" or "druid" or "paladin" or "wizard" || Spellcasting?.Preparation == ClassSpellcastingInfo.Prepared;
}

/// <summary>A save DC of a class other than the spell one ("CD de bombas"): 8 + proficiency bonus + <see cref="Ability"/>.</summary>
public sealed record ClassSaveDc(string Name, string Ability)
{
    public const int NameMaxLength = 100;

    /// <summary>Tolerant: malformed JSON or entries give an empty list.</summary>
    public static IReadOnlyList<ClassSaveDc> ParseAll(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != System.Text.Json.JsonValueKind.Array)
            {
                return [];
            }

            var result = new List<ClassSaveDc>();
            foreach (var entry in document.RootElement.EnumerateArray())
            {
                if (entry.ValueKind == System.Text.Json.JsonValueKind.Object
                    && entry.TryGetProperty("name", out var name) && name.ValueKind == System.Text.Json.JsonValueKind.String
                    && entry.TryGetProperty("ability", out var ability) && ability.ValueKind == System.Text.Json.JsonValueKind.String
                    && OpenTrpg.Core.Domain.Characters.Abilities.IsValid(ability.GetString()!))
                {
                    result.Add(new ClassSaveDc(name.GetString()!, ability.GetString()!));
                }
            }

            return result;
        }
        catch (System.Text.Json.JsonException)
        {
            return [];
        }
    }
}
