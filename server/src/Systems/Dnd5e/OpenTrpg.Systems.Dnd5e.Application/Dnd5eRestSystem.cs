using System.Text.Json;
using System.Text.Json.Nodes;
using OpenTrpg.Core.Application.Characters;
using OpenTrpg.Core.Application.Common;
using OpenTrpg.Core.Domain.Characters;
using OpenTrpg.Core.Domain.Rules;
using OpenTrpg.Core.Application;
using OpenTrpg.Core.Application.Systems;
using OpenTrpg.Systems.Dnd5e.Application;
using OpenTrpg.Systems.Dnd5e.Application.Characters;
using OpenTrpg.Systems.Dnd5e.Domain.Characters;
using OpenTrpg.Systems.Dnd5e.Domain.Rules;

namespace OpenTrpg.Systems.Dnd5e.Application;

/// <summary>
/// Short and long rests asked to the DM (<see cref="IRestSystem"/>). A short rest names the hit dice to spend
/// (<c>hitDice</c>), which must not exceed the remaining ones now; approval spends what remains then (PHB rules).
/// </summary>
public sealed class Dnd5eRestSystem(
    Dnd5eCharacterParts parts,
    ICharacterSheetService sheets,
    CompanionPlanner companions,
    IDiceRoller dice) : IRestSystem
{
    public const int MaxClasses = 20;

    public IReadOnlyList<string> Kinds { get; } = [Dnd5eRestPayload.KindName(RestKind.Short), Dnd5eRestPayload.KindName(RestKind.Long)];

    public async Task<JsonObject> ValidateRequestAsync(CharacterRef character, string kind, JsonElement payload, CancellationToken cancellationToken = default)
    {
        var restKind = Parse(kind);
        var requested = ReadHitDice(payload);
        if (requested is not null)
        {
            if (requested.Count > MaxClasses)
            {
                throw AppException.Validation("hitDice", $"No se admiten más de {MaxClasses} clases.");
            }

            if (!requested.All(e => !string.IsNullOrWhiteSpace(e.Key) && e.Value is >= 0 and <= Dnd5eRestPayload.MaxHitDicePerClass))
            {
                throw AppException.Validation("hitDice", $"Cada clase debe indicar entre 0 y {Dnd5eRestPayload.MaxHitDicePerClass} dados de golpe.");
            }

            if (restKind == RestKind.Long && requested.Values.Any(v => v != 0))
            {
                throw AppException.Validation("hitDice", "El descanso largo no gasta dados de golpe.");
            }
        }

        var loaded = await parts.LoadAsync(character, cancellationToken);
        var hitDice = restKind == RestKind.Short ? requested ?? [] : new Dictionary<string, int>();
        foreach (var (classIndex, count) in hitDice)
        {
            var index = classIndex.Trim();
            if (count > 0 && !loaded.Classes.Any(c => c.ClassIndex == index))
            {
                throw AppException.Validation("hitDice", $"El personaje no tiene la clase '{index}'.");
            }

            if (count > loaded.HitDiceRemaining(index))
            {
                throw AppException.Validation("hitDice", $"No quedan suficientes dados de golpe de '{index}'.");
            }
        }

        return JsonNode.Parse(Dnd5eRestPayload.Serialize(Dnd5eRestPayload.Normalize(restKind, hitDice))) as JsonObject ?? [];
    }

    public async Task ApplyAsync(CharacterRef character, string kind, JsonElement payload, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        var loaded = await parts.LoadAsync(character, cancellationToken);
        var sheet = await sheets.CalculateAsync(loaded, cancellationToken);
        if (Parse(kind) == RestKind.Short)
        {
            loaded.ShortRest(loaded.ClampHitDiceToRemaining(ReadHitDice(payload) ?? []), sheet, dice, now);
        }
        else
        {
            loaded.LongRest(sheet, now);
            await companions.RestoreAfterLongRestAsync([loaded], now, cancellationToken);
        }
    }

    private static RestKind Parse(string kind) =>
        Enum.TryParse<RestKind>(kind, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed)
            ? parsed
            : throw AppException.Validation("kind", "El descanso debe ser short o long.");

    /// <summary>The <c>hitDice</c> of a payload, or null when absent.</summary>
    private static Dictionary<string, int>? ReadHitDice(JsonElement payload)
    {
        if (payload.ValueKind != JsonValueKind.Object || !payload.TryGetProperty("hitDice", out var hitDice) || hitDice.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        try
        {
            return hitDice.Deserialize<Dictionary<string, int>>();
        }
        catch (JsonException)
        {
            throw AppException.Validation("hitDice", "Los dados de golpe deben indicar un número por clase.");
        }
    }
}
