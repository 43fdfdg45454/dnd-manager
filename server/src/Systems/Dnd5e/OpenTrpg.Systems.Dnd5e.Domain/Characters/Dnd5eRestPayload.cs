using System.Text.Json;
using OpenTrpg.Core.Domain.Common;
using OpenTrpg.Core.Domain.Characters;
using OpenTrpg.Systems.Dnd5e.Domain.Characters;

namespace OpenTrpg.Systems.Dnd5e.Domain.Characters;

/// <summary>
/// The D&amp;D 5e payload of a rest request (<see cref="RestRequest.PayloadJson"/>): the hit dice a short rest spends
/// per class index, <c>{"hitDice":{"fighter":2}}</c> (always empty for a long rest).
/// </summary>
public static class Dnd5eRestPayload
{
    /// <summary>Upper bound of dice per class in a request (a character never has more than 20 levels).</summary>
    public const int MaxHitDicePerClass = 20;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>The kind stored in <see cref="RestRequest.Kind"/>.</summary>
    public static string KindName(RestKind kind) => kind.ToString();

    /// <summary>The rest kind of a request, or null when it is not a D&amp;D 5e kind.</summary>
    public static RestKind? Kind(RestRequest request) =>
        Enum.TryParse<RestKind>(request.Kind, ignoreCase: false, out var kind) ? kind : null;

    /// <summary>
    /// The dice to spend, checked: entries with 0 dice are dropped and class indexes are trimmed; a long rest spends
    /// no hit dice.
    /// </summary>
    public static Dictionary<string, int> Normalize(RestKind kind, IReadOnlyDictionary<string, int>? hitDice)
    {
        var dice = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var (classIndex, count) in hitDice ?? new Dictionary<string, int>())
        {
            if (string.IsNullOrWhiteSpace(classIndex))
            {
                throw DomainException.RuleViolation("Indica la clase de cada dado de golpe.");
            }

            if (count is < 0 or > MaxHitDicePerClass)
            {
                throw DomainException.RuleViolation($"Cada clase debe indicar entre 0 y {MaxHitDicePerClass} dados de golpe.");
            }

            if (count > 0)
            {
                var key = classIndex.Trim();
                dice[key] = dice.GetValueOrDefault(key) + count;
            }
        }

        if (kind == RestKind.Long && dice.Count > 0)
        {
            throw DomainException.RuleViolation("El descanso largo no gasta dados de golpe.");
        }

        return dice;
    }

    /// <summary>The payload JSON of the dice (see <see cref="Normalize"/>).</summary>
    public static string Serialize(IReadOnlyDictionary<string, int> hitDice) =>
        JsonSerializer.Serialize(new Payload(new Dictionary<string, int>(hitDice, StringComparer.Ordinal)), JsonOptions);

    /// <summary>The hit dice of a request (empty when it has none).</summary>
    public static IReadOnlyDictionary<string, int> HitDice(RestRequest request)
    {
        try
        {
            return JsonSerializer.Deserialize<Payload>(request.PayloadJson, JsonOptions)?.HitDice ?? [];
        }
        catch (JsonException)
        {
            return new Dictionary<string, int>();
        }
    }

    private sealed record Payload(Dictionary<string, int>? HitDice);
}
