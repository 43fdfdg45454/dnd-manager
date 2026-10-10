using System.Text.Json;
using System.Text.Json.Nodes;

namespace OpenTrpg.Core.Application.Systems;

/// <summary>
/// Conversions between the JSON objects of the game systems and the <c>[JsonExtensionData]</c> dictionaries with which
/// the core DTOs write the system's fields at the same level as their own.
/// </summary>
public static class JsonFields
{
    public static Dictionary<string, JsonElement> From(JsonObject? fields)
    {
        var result = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var (key, value) in fields ?? [])
        {
            result[key] = JsonSerializer.SerializeToElement(value);
        }

        return result;
    }

    public static JsonObject ToObject(IReadOnlyDictionary<string, JsonElement>? fields)
    {
        var result = new JsonObject();
        foreach (var (key, value) in fields ?? new Dictionary<string, JsonElement>())
        {
            result[key] = JsonNode.Parse(value.GetRawText());
        }

        return result;
    }

    public static JsonElement ToElement(IReadOnlyDictionary<string, JsonElement>? fields) =>
        JsonSerializer.SerializeToElement(fields ?? new Dictionary<string, JsonElement>());
}
