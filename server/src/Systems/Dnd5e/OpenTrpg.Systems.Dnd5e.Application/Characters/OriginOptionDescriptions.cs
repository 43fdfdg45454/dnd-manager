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

    /// <summary>
    /// The catalog paragraphs of a tool or instrument (empty when it has none or it is not in the catalog): what a tool
    /// proficiency means is said once, in the <see cref="ToolChoiceNote"/> of the choice.
    /// </summary>
    public static IReadOnlyList<string> Tool(ItemTemplate? item) =>
        item?.Description.Where(d => !string.IsNullOrWhiteSpace(d)).ToList() ?? [];

    /// <summary>Note of a tool proficiency choice: how many to pick and what the proficiency gives (Spanish).</summary>
    public static string ToolChoiceNote(int choose) =>
        $"Eliges competencia con {(choose == 1 ? "una" : choose.ToString(System.Globalization.CultureInfo.InvariantCulture))} de estas herramientas: "
        + "sumas tu bonificador de competencia a las pruebas que hagas con ellas.";

    /// <summary>
    /// Summary of a catalog weapon ("Arma marcial cuerpo a cuerpo · 1d8 cortante · versátil (1d10)", with its range when
    /// it has one), then its properties ("Propiedades: finesse, light.") when any and the item's own paragraphs.
    /// </summary>
    public static IReadOnlyList<string> Weapon(ItemTemplate item)
    {
        var summary = new List<string> { WeaponCategory(item.Subcategory) };
        if (!string.IsNullOrWhiteSpace(item.DamageDice))
        {
            var type = string.IsNullOrWhiteSpace(item.DamageType) ? null : DamageTypes.Spanish(item.DamageType.Trim().ToLowerInvariant()).ToLowerInvariant();
            summary.Add(type is null ? item.DamageDice : $"{item.DamageDice} {type}");
        }

        if (!string.IsNullOrWhiteSpace(item.VersatileDice))
        {
            summary.Add($"versátil ({item.VersatileDice})");
        }

        if (item.RangeNormal is { } normal)
        {
            summary.Add(item.RangeLong is { } far ? $"alcance {normal}/{far}" : $"alcance {normal}");
        }

        var lines = new List<string> { string.Join(" · ", summary) };
        var properties = item.Properties.Where(p => !string.IsNullOrWhiteSpace(p)).ToList();
        if (properties.Count > 0)
        {
            lines.Add($"Propiedades: {string.Join(", ", properties)}.");
        }

        lines.AddRange(item.Description.Where(d => !string.IsNullOrWhiteSpace(d)));
        return lines;
    }

    /// <summary>"Arma sencilla cuerpo a cuerpo" for the "Simple Melee" subcategory; just "Arma" when it says nothing more.</summary>
    private static string WeaponCategory(string? subcategory)
    {
        var parts = new List<string> { "Arma" };
        foreach (var word in (subcategory ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var spanish = word.ToLowerInvariant() switch
            {
                "simple" => "sencilla",
                "martial" => "marcial",
                "exotic" => "exótica",
                "melee" => "cuerpo a cuerpo",
                "ranged" => "a distancia",
                _ => null,
            };
            if (spanish is not null)
            {
                parts.Add(spanish);
            }
        }

        return string.Join(' ', parts);
    }

    /// <summary>The skill text of the catalog or, when it is empty, the ability it uses.</summary>
    public static IReadOnlyList<string> Skill(SkillDefinition? skill, string name) =>
        skill is { Description.Count: > 0 }
            ? skill.Description
            : [skill is null ? $"Habilidad: {name}." : $"Habilidad basada en {BreakdownLabels.Ability(skill.AbilityIndex)}."];
}
