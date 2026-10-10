using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenTrpg.Systems.Dnd5e.Domain.Catalog;

/// <summary>
/// Spellcasting details of a class defined by a content pack (format 3 <c>classes[].spellcasting</c>). The ability, the
/// multiclass contribution and the slots per level live in <see cref="ClassDefinition"/> and <see cref="ClassLevel"/>;
/// this keeps what the sheet and the wizard read on top of them.
/// </summary>
/// <param name="Progression">"full", "half", "third", "pact" or "table".</param>
/// <param name="Preparation">"prepared" (spells prepared from the whole list each day) or "known".</param>
/// <param name="Ritual">Whether the class casts its ritual spells as rituals.</param>
/// <param name="Focus">Spellcasting focus (free text or a tool index), or null.</param>
public sealed record ClassSpellcastingInfo(string Progression, string Preparation, bool Ritual, string? Focus)
{
    public const string Full = "full";
    public const string Half = "half";
    public const string Third = "third";
    public const string Pact = "pact";
    public const string Table = "table";
    public const string Prepared = "prepared";
    public const string Known = "known";

    public static readonly IReadOnlyList<string> Progressions = [Full, Half, Third, Pact, Table];

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>Contribution to the multiclass spellcaster level: 1 full and pact, 2 half, 3 third, 0 for a table.</summary>
    public static int SpellcastingLevelOf(string progression) => progression switch
    {
        Full or Pact => 1,
        Half => 2,
        Third => 3,
        _ => 0,
    };

    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);

    public static ClassSpellcastingInfo? Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<ClassSpellcastingInfo>(json, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

/// <summary>
/// Multiclassing of a class defined by a content pack (<c>classes[].multiclassing</c>): minimum scores and the
/// proficiencies gained when the class is taken as a new class.
/// </summary>
/// <param name="Prerequisites">Minimum score by ability index; every one must be met.</param>
/// <param name="Armor">Armor proficiencies ("light-armor", "shields").</param>
/// <param name="Weapons">Weapon proficiencies ("simple-weapons", a weapon index).</param>
/// <param name="Tools">Tool proficiencies (indexes or names).</param>
/// <param name="Skills">Skills chosen from the class list when the class is taken as a new class (0 for none).</param>
public sealed record ClassMulticlassing(
    IReadOnlyDictionary<string, int> Prerequisites,
    IReadOnlyList<string> Armor,
    IReadOnlyList<string> Weapons,
    IReadOnlyList<string> Tools,
    int Skills)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);

    public static ClassMulticlassing? Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<ClassMulticlassing>(json, JsonOptions);
            return parsed is null
                ? null
                : parsed with
                {
                    Prerequisites = parsed.Prerequisites ?? new Dictionary<string, int>(),
                    Armor = parsed.Armor ?? [],
                    Weapons = parsed.Weapons ?? [],
                    Tools = parsed.Tools ?? [],
                };
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

/// <summary>
/// Spell list of a class defined by a content pack beyond the spells that name it in their <c>classes</c>/<c>lists</c>:
/// explicit spell indexes and classes whose whole list it inherits (<c>{"class": "wizard"}</c>).
/// </summary>
public sealed record ClassSpellList(IReadOnlyList<string> Spells, IReadOnlyList<string> Classes)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);

    /// <summary>Whether a spell (with the class lists it names) belongs to this list.</summary>
    public bool Contains(string spellIndex, IReadOnlyList<string> spellClasses) =>
        Spells.Contains(spellIndex, StringComparer.Ordinal) || Classes.Any(c => spellClasses.Contains(c, StringComparer.Ordinal));

    public static ClassSpellList? Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<ClassSpellList>(json, JsonOptions);
            return parsed is null ? null : new ClassSpellList(parsed.Spells ?? [], parsed.Classes ?? []);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
