using Dnd.Application.Abstractions.Persistence;

namespace Dnd.Application.Catalog;

/// <summary>Every race of the catalog, ordered by name.</summary>
public sealed class ListRacesHandler(ICatalogRepository catalog)
{
    public async Task<IReadOnlyList<RaceSummaryDto>> HandleAsync(CancellationToken cancellationToken = default) =>
        (await catalog.ListRacesAsync(cancellationToken))
            .Select(r => new RaceSummaryDto(r.Index, r.Name, r.Speed, r.Size, CatalogJson.AbilityBonuses(r.AbilityBonusesJson), r.SubraceIndexes, r.Source))
            .ToList();
}
