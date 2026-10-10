using OpenTrpg.Core.Domain.Catalog;
using OpenTrpg.Core.Domain.Characters;
using OpenTrpg.Core.Application;
using OpenTrpg.Core.Application.Characters;
using OpenTrpg.Systems.Dnd5e.Application;
using OpenTrpg.Systems.Dnd5e.Application.Characters;
using OpenTrpg.Systems.Dnd5e.Domain.Catalog;
using OpenTrpg.Systems.Dnd5e.Domain.Characters;

namespace OpenTrpg.Systems.Dnd5e.Application.Characters;

/// <summary>
/// Descriptions of the origin options whose catalog entry has no text of its own (phase 29): abilities, languages
/// and tools. Every option of <see cref="OriginChoicesPlanner"/> carries at least one paragraph.
/// </summary>
public static class OriginOptionDescriptions
{
    /// <summary>One SRD language: standard or exotic, typical speakers and script (null when it has none).</summary>
    public sealed record LanguageInfo(string Name, bool Exotic, string Speakers, string? Script, string? Description);

    /// <summary>
    /// The sixteen languages of the SRD (<c>seed/srd/5e-SRD-Languages.json</c>) with their speakers and script in
    /// Spanish; <see cref="LanguageInfo.Description"/> keeps the SRD text in English when the dataset has one. A test
    /// checks this table against the dataset.
    /// </summary>
    public static IReadOnlyList<LanguageInfo> Languages { get; } =
    [
        new("Common", false, "humanos", "común", null),
        new("Dwarvish", false, "enanos", "enana", "Dwarvish is full of hard consonants and guttural sounds."),
        new("Elvish", false, "elfos", "élfica", "Elvish is fluid, with subtle intonations and intricate grammar. Elven literature is rich and varied, and their songs and poems are famous among other races. Many bards learn their language so they can add Elvish ballads to their repertoires."),
        new("Giant", false, "ogros y gigantes", "enana", null),
        new("Gnomish", false, "gnomos", "enana", "The Gnomish language, which uses the Dwarvish script, is renowned for its technical treatises and its catalogs of knowledge about the natural world."),
        new("Goblin", false, "goblinoides", "enana", null),
        new("Halfling", false, "medianos", "común", "The Halfling language isn't secret, but halflings are loath to share it with others. They write very little, so they don't have a rich body of literature. Their oral tradition, however, is very strong."),
        new("Orc", false, "orcos", "enana", "Orc is a harsh, grating language with hard consonants. It has no script of its own but is written in the Dwarvish script."),
        new("Abyssal", true, "demonios", "infernal", null),
        new("Celestial", true, "celestiales", "celestial", null),
        new("Draconic", true, "dragones y dracónidos", "dracónica", "Draconic is thought to be one of the oldest languages and is often used in the study of magic. The language sounds harsh to most other creatures and includes numerous hard consonants and sibilants."),
        new("Deep Speech", true, "aboleths y capas acechadoras", null, null),
        new("Infernal", true, "diablos", "infernal", null),
        new("Primordial", true, "elementales", "enana", null),
        new("Sylvan", true, "criaturas feéricas", "élfica", null),
        new("Undercommon", true, "comerciantes de la Infraoscuridad", "élfica", null),
    ];

    private static readonly Dictionary<string, LanguageInfo> LanguagesByKey = Languages
        .SelectMany(l => new[] { (Key: l.Name, Info: l), (Key: l.Name.Replace(' ', '-'), Info: l) })
        .DistinctBy(p => p.Key, StringComparer.OrdinalIgnoreCase)
        .ToDictionary(p => p.Key, p => p.Info, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// "Idioma estándar. Hablantes típicos: humanos. Escritura común.", followed by the SRD text when there is one;
    /// a language outside the SRD (a content pack) gets a generic line.
    /// </summary>
    public static IReadOnlyList<string> Language(string nameOrIndex)
    {
        if (!LanguagesByKey.TryGetValue(nameOrIndex, out var info))
        {
            return ["Idioma adicional que el personaje sabe hablar, leer y escribir."];
        }

        var summary = $"Idioma {(info.Exotic ? "exótico" : "estándar")}. Hablantes típicos: {info.Speakers}. "
            + (info.Script is null ? "Sin escritura." : $"Escritura {info.Script}.");
        return info.Description is null ? [summary] : [summary, info.Description];
    }

    private static readonly Dictionary<string, string> AbilityTexts = new(StringComparer.Ordinal)
    {
        ["str"] = "Mide la potencia física, el entrenamiento atlético y la fuerza bruta que puedes ejercer.",
        ["dex"] = "Mide la agilidad, los reflejos y el equilibrio.",
        ["con"] = "Mide la salud, el aguante y la fuerza vital.",
        ["int"] = "Mide la agudeza mental, la memoria y la capacidad de razonar.",
        ["wis"] = "Mide la percepción y la intuición: lo atento que estás al mundo que te rodea.",
        ["cha"] = "Mide la capacidad de relacionarte con los demás: seguridad, elocuencia y presencia.",
    };

    /// <summary>What the ability measures (SRD "Using Ability Scores"), in Spanish.</summary>
    public static IReadOnlyList<string> Ability(string ability) =>
        [AbilityTexts.GetValueOrDefault(ability, "Característica del personaje.")];

    private static readonly Dictionary<string, string> ToolCategories = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Artisan's Tools"] = "Herramientas de artesano",
        ["Gaming Sets"] = "Juego",
        ["Musical Instrument"] = "Instrumento musical",
        ["Other Tools"] = "Herramientas",
    };

    /// <summary>
    /// The description of the catalog item of a tool or instrument or, when it has none, its category
    /// ("Herramientas de artesano"); a tool outside the catalog gets a generic line.
    /// </summary>
    public static IReadOnlyList<string> Tool(ItemTemplate? item)
    {
        if (item is null)
        {
            return ["Competencia con estas herramientas: sumas tu bonificador de competencia a las pruebas que hagas con ellas."];
        }

        if (item.Description.Any(d => !string.IsNullOrWhiteSpace(d)))
        {
            return item.Description;
        }

        return [string.IsNullOrWhiteSpace(item.Subcategory) ? "Herramientas" : ToolCategories.GetValueOrDefault(item.Subcategory, item.Subcategory)];
    }

    /// <summary>The skill text of the catalog or, when it is empty, the ability it uses.</summary>
    public static IReadOnlyList<string> Skill(SkillDefinition? skill, string name) =>
        skill is { Description.Count: > 0 }
            ? skill.Description
            : [skill is null ? $"Habilidad: {name}." : $"Habilidad basada en {BreakdownLabels.Ability(skill.AbilityIndex)}."];
}
