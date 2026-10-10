using OpenTrpg.Core.Application.Abstractions.Persistence;

namespace OpenTrpg.Core.Application.Catalog;

/// <summary>One result of the d100 trinket table, resolved to its catalog item.</summary>
/// <param name="TemplateId">Item template to add to the inventory.</param>
/// <param name="Description">Paragraphs of the item joined with blank lines.</param>
public sealed record TrinketDto(int Roll, Guid TemplateId, string Index, string Name, string Description);

/// <summary>
/// Trinket table of the instance (<c>GET /catalog/trinkets</c>). It only comes from content packs (the table is not in
/// the SRD), so it is empty on a pure SRD instance; entries whose item is no longer in the catalog are left out.
/// </summary>
public sealed class ListTrinketsHandler(ICatalogRepository catalog)
{
    public async Task<IReadOnlyList<TrinketDto>> HandleAsync(CancellationToken cancellationToken = default)
    {
        var entries = await catalog.ListTrinketsAsync(cancellationToken);
        if (entries.Count == 0)
        {
            return [];
        }

        var items = (await catalog.ListCatalogItemsByIndexAsync(entries.Select(e => e.ItemIndex).Distinct().ToList(), cancellationToken))
            .GroupBy(i => i.Index!, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        return entries
            .Where(e => items.ContainsKey(e.ItemIndex))
            .Select(e =>
            {
                var item = items[e.ItemIndex];
                return new TrinketDto(e.Roll, item.Id, e.ItemIndex, item.Name, string.Join("\n\n", item.Description));
            })
            .ToList();
    }
}
