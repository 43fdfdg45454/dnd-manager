using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenTrpg.Systems.Dnd5e.Domain.Catalog;

/// <summary>
/// Spellcasting details of a class defined by a content pack (format 3 <c>classes[].spellcasting</c>). The ability, the
/// multiclass contribution and the slots per level live in <see cref="ClassDefinition"/> and <see cref="ClassLevel"/>;
/// this keeps what the sheet and the wizard read on top of them.
/// </summary>
/// <param name="Progression">"full", "half", "third", "pact", "table", "hitPoints" (no slots: each spell costs a use
/// and hit points) or "ritualsOnly" (no slots: casts the spells of its book only as rituals).</param>
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
    public const string HitPoints = "hitPoints";
    public const string RitualsOnly = "ritualsOnly";
    public const string Prepared = "prepared";
    public const string Known = "known";

    public static readonly IReadOnlyList<string> Progressions = [Full, Half, Third, Pact, Table, HitPoints, RitualsOnly];

    /// <summary>Highest spell level by class level (entries where it changes) of the progressions without slots.</summary>
    [JsonPropertyName("maxSpellLevel")]
    public IReadOnlyDictionary<int, int>? MaxSpellLevels { get; init; }

    /// <summary>"hitPoints": key of the class resource that counts the spells cast.</summary>
    public string? UsesResource { get; init; }

    /// <summary>"hitPoints": hit points lost to cast a spell of each level (index 0 = 1st level).</summary>
    public IReadOnlyList<int>? HitPointCost { get; init; }

    /// <summary>Spells prepared, as a formula ("mod:wis+halfClassLevel", see <see cref="PreparedFormula"/>); null = the standard rule.</summary>
    [JsonPropertyName("preparedFormula")]
    public string? PreparedFormulaText { get; init; }

    /// <summary>Spells the class casts as rituals although they are not rituals.</summary>
    public IReadOnlyList<string>? TreatAsRitual { get; init; }

    /// <summary>The progression has no spell slots (<see cref="HitPoints"/> or <see cref="RitualsOnly"/>).</summary>
    [JsonIgnore]
    public bool WithoutSlots => Progression is HitPoints or RitualsOnly;

    /// <summary>Highest spell level at a class level from <see cref="MaxSpellLevels"/>; null when the class uses its slots.</summary>
    public int? MaxSpellLevelAt(int classLevel) =>
        MaxSpellLevels is { Count: > 0 } table ? SubclassSpellcasting.At(table, classLevel) : null;

    /// <summary>Hit points to cast a spell of <paramref name="spellLevel"/> (1-9); null when it cannot be cast that way.</summary>
    public int? HitPointCostOf(int spellLevel) =>
        HitPointCost is { } cost && spellLevel >= 1 && spellLevel <= cost.Count ? cost[spellLevel - 1] : null;

    /// <summary>Whether the class casts <paramref name="spellIndex"/> as a ritual (the spell is one, or the class treats it as one).</summary>
    public bool CastsAsRitual(string spellIndex, bool isRitual) =>
        isRitual || (TreatAsRitual?.Contains(spellIndex, StringComparer.Ordinal) ?? false);

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>Contribution to the multiclass spellcaster level: 1 full and pact, 2 half, 3 third, 0 for a table and the progressions without slots.</summary>
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

    /// <summary>
    /// Alternatives on top of <see cref="Prerequisites"/>: one of the maps must be met entirely
    /// (<c>[{"str": 13}, {"dex": 13}]</c> reads "Fuerza 13 o Destreza 13").
    /// </summary>
    public IReadOnlyList<IReadOnlyDictionary<string, int>> AnyOf { get; init; } = [];

    /// <summary>
    /// Proficiencies a character that took the class as a new class gains on reaching a level of it
    /// (<c>{2: {armor: [heavy-armor]}}</c>); the first class of the character never gains them.
    /// </summary>
    public IReadOnlyDictionary<int, MulticlassLevelProficiencies> ProficienciesByLevel { get; init; } =
        new Dictionary<int, MulticlassLevelProficiencies>();

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
                    AnyOf = parsed.AnyOf ?? [],
                    ProficienciesByLevel = parsed.ProficienciesByLevel ?? new Dictionary<int, MulticlassLevelProficiencies>(),
                };
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

/// <summary>Armor, weapon and tool proficiencies gained at one level of a class taken as a new class (normalized keys).</summary>
public sealed record MulticlassLevelProficiencies(IReadOnlyList<string> Armor, IReadOnlyList<string> Weapons, IReadOnlyList<string> Tools);

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
