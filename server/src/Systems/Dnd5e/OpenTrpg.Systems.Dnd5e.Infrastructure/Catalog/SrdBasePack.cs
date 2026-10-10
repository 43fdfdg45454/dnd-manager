using System.Text.Json;
using OpenTrpg.Systems.Dnd5e.Domain.Catalog;

namespace OpenTrpg.Systems.Dnd5e.Infrastructure.Catalog;

/// <summary>
/// The SRD 5.1 as base content pack of the module (format 3), embedded in this assembly from
/// <c>server/src/Systems/Dnd5e/seed/srd-5.1.pack.json</c> (generated with <c>server/tools/SrdPack</c>).
/// </summary>
internal static class SrdBasePack
{
    private const string ResourceName = "OpenTrpg.Systems.Dnd5e.srd-5.1.pack.json";

    private static readonly Lazy<IReadOnlyDictionary<string, IReadOnlyList<StartingItem>>> ItemContentsCache = new(LoadItemContents);

    /// <summary>
    /// Contents of the SRD equipment packs by item index ("explorers-pack" → backpack, bedroll...), for the starting
    /// equipment of content packs that give them.
    /// </summary>
    public static IReadOnlyDictionary<string, IReadOnlyList<StartingItem>> ItemContents => ItemContentsCache.Value;

    public static Stream Open() =>
        typeof(SrdBasePack).Assembly.GetManifestResourceStream(ResourceName)
        ?? throw new InvalidOperationException($"The SRD base pack '{ResourceName}' is not embedded in {typeof(SrdBasePack).Assembly.GetName().Name}.");

    private static IReadOnlyDictionary<string, IReadOnlyList<StartingItem>> LoadItemContents()
    {
        using var stream = Open();
        using var document = JsonDocument.Parse(stream);
        var result = new Dictionary<string, IReadOnlyList<StartingItem>>(StringComparer.Ordinal);
        if (!document.RootElement.TryGetProperty("items", out var items))
        {
            return result;
        }

        foreach (var item in items.EnumerateArray())
        {
            if (item.TryGetProperty("index", out var index) && item.TryGetProperty("contents", out var contents))
            {
                result[index.GetString()!] = contents.EnumerateArray()
                    .Select(c => new StartingItem(
                        c.GetProperty("item").GetString()!,
                        c.TryGetProperty("quantity", out var quantity) ? Math.Max(1, quantity.GetInt32()) : 1,
                        c.TryGetProperty("name", out var name) ? name.GetString() : null))
                    .ToList();
            }
        }

        return result;
    }
}
