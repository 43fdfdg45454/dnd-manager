using System.Text.Json.Nodes;

namespace OpenTrpg.Systems.Dnd5e.Api.Tests.ContentPacks;

/// <summary>
/// Fictitious format-3 pack for the creation and level-up gaps (phase 35B1): every class, option and item is
/// invented ("Cronista del vacío", "Guadaña doble"...). Each test changes a copy when it needs a broken variant.
/// </summary>
internal static class CreationPack
{
    public const string Id = "vacio-prueba";

    /// <summary>Index with the pack prefix.</summary>
    public static string I(string name) => $"{Id}-{name}";

    /// <summary>The whole pack, as the tests import it.</summary>
    public static JsonObject Build() => new()
    {
        ["formatVersion"] = 3,
        ["system"] = "dnd5e",
        ["id"] = Id,
        ["name"] = "Crónicas del vacío (prueba)",
        ["version"] = "1.0.0",
        ["classes"] = new JsonArray(),
        ["items"] = Items(),
    };

    /// <summary>A copy of <paramref name="pack"/> with another id and prefix, for the error tests.</summary>
    public static JsonObject Renamed(JsonObject pack, string id)
    {
        var json = pack.ToJsonString().Replace($"\"{Id}\"", $"\"{id}\"", StringComparison.Ordinal)
            .Replace($"{Id}-", $"{id}-", StringComparison.Ordinal);
        return JsonNode.Parse(json)!.AsObject();
    }

    /// <summary>Twenty levels with no features; <paramref name="features"/> adds features by level.</summary>
    public static JsonArray Levels(Func<int, JsonObject?>? level = null)
    {
        var levels = new JsonArray();
        for (var number = 1; number <= 20; number++)
        {
            levels.Add(level?.Invoke(number) ?? new JsonObject { ["level"] = number });
        }

        return levels;
    }

    private static JsonArray Items() =>
    [
        new JsonObject
        {
            ["index"] = I("guadana-doble"),
            ["name"] = "Guadaña doble",
            ["costCp"] = 3000,
            ["weightLb"] = 8,
            ["weapon"] = new JsonObject { ["category"] = "exotic", ["range"] = "melee", ["damage"] = "1d10", ["damageType"] = "slashing", ["properties"] = new JsonArray("two-handed") },
        },
        new JsonObject
        {
            ["index"] = I("petardo"),
            ["name"] = "Petardo del vacío",
            ["costCp"] = 500,
            ["weightLb"] = 1,
            ["weapon"] = new JsonObject { ["category"] = "simple", ["range"] = "ranged", ["damage"] = "2d6", ["damageType"] = "fire", ["properties"] = new JsonArray("thrown", "bomb"), ["rangeNormal"] = 20, ["rangeLong"] = 60 },
        },
        new JsonObject
        {
            ["index"] = I("trabuco"),
            ["name"] = "Trabuco de bruma",
            ["costCp"] = 20000,
            ["weightLb"] = 6,
            ["weapon"] = new JsonObject { ["category"] = "exotic", ["range"] = "ranged", ["damage"] = "1d12", ["damageType"] = "piercing", ["properties"] = new JsonArray("loading"), ["rangeNormal"] = 40, ["rangeLong"] = 120 },
            ["firearm"] = new JsonObject { ["reload"] = 1, ["misfire"] = 3 },
        },
        new JsonObject
        {
            ["index"] = I("coraza-de-runas"),
            ["name"] = "Coraza de runas",
            ["costCp"] = 40000,
            ["weightLb"] = 30,
            ["armor"] = new JsonObject { ["category"] = "medium", ["baseAc"] = 15, ["exotic"] = true },
        },
    ];
}
