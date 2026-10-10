using System.Text.Json;
using System.Text.Json.Serialization;
using OpenTrpg.Core.Domain.Characters;
using OpenTrpg.Systems.Dnd5e.Domain.Characters;

namespace OpenTrpg.Systems.Dnd5e.Domain.Characters;

/// <summary>
/// What a character chose when it gained a class level (phase 16c): the answer to one level choice rule
/// (<c>LevelChoiceRule</c>), or the hit points rolled (<see cref="HitPointsKey"/>). <see cref="SelectedJson"/>
/// is a self-describing <see cref="ChoiceSelection"/> with the names of what was picked, so the sheet can
/// show it even if the catalog changes.
/// </summary>
public sealed class CharacterChoice
{
    /// <summary>Key of the hit points rolled at a class level (<see cref="ChoiceSelection.Roll"/>).</summary>
    public const string HitPointsKey = "hit-points";

    private CharacterChoice()
    {
    }

    public Guid Id { get; init; } = Guid.NewGuid();

    public Guid CharacterId { get; private set; }

    /// <summary>Level of the class (<see cref="ClassIndex"/>) at which the choice was made; 0 for origin choices (race, background).</summary>
    public int Level { get; private set; }

    /// <summary>Class of the choice; null for origin choices (keys <c>race.*</c> and <c>background.*</c>).</summary>
    public string? ClassIndex { get; private set; }

    /// <summary>A race or background choice made at creation (level 0, no class).</summary>
    public bool IsOrigin => ClassIndex is null;

    /// <summary>Key of the rule ("subclass", "asi", "eldritch-invocations"...) or <see cref="HitPointsKey"/>.</summary>
    public string Key { get; private set; } = string.Empty;

    /// <summary>Persisted <see cref="ChoiceSelection"/>.</summary>
    public string SelectedJson { get; private set; } = "{}";

    public DateTimeOffset CreatedAt { get; private set; }

    public ChoiceSelection Selection => ChoiceSelection.Parse(SelectedJson);

    internal static CharacterChoice Create(Guid characterId, int level, string? classIndex, string key, ChoiceSelection selection, DateTimeOffset now) => new()
    {
        CharacterId = characterId,
        Level = level,
        ClassIndex = classIndex,
        Key = key,
        SelectedJson = selection.ToJson(),
        CreatedAt = now,
    };
}

/// <summary>An option, spell, skill or subclass picked in a level choice, with its name at the time.</summary>
public sealed record ChoiceItem(string Index, string Name);

/// <summary>
/// The answer to a level choice. <see cref="Kind"/> is a <c>LevelChoiceKind</c> name or <see cref="HitPointsKind"/>.
/// Picks: <see cref="Selected"/> (and, for replaceable choices, <see cref="Replaced"/>: earlier picks swapped
/// out); Ability Score Improvement: <see cref="Asi"/> ("str" → 1); feat: <see cref="Feat"/> and the
/// <see cref="Ability"/> it raises; hit points: <see cref="Roll"/>.
/// </summary>
public sealed record ChoiceSelection
{
    public const string HitPointsKind = "HitPoints";

    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public string Kind { get; init; } = string.Empty;

    /// <summary>Name of the rule ("Fighting Style").</summary>
    public string Name { get; init; } = string.Empty;

    public string? SetId { get; init; }

    public IReadOnlyList<ChoiceItem> Selected { get; init; } = [];

    public IReadOnlyList<ChoiceItem> Replaced { get; init; } = [];

    public IReadOnlyDictionary<string, int>? Asi { get; init; }

    public ChoiceItem? Feat { get; init; }

    public string? Ability { get; init; }

    public int? Roll { get; init; }

    public string ToJson() => JsonSerializer.Serialize(this, Options);

    /// <summary>Tolerant: malformed JSON gives an empty selection.</summary>
    public static ChoiceSelection Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new ChoiceSelection();
        }

        try
        {
            return JsonSerializer.Deserialize<ChoiceSelection>(json, Options) ?? new ChoiceSelection();
        }
        catch (JsonException)
        {
            return new ChoiceSelection();
        }
    }
}
