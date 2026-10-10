using OpenTrpg.Core.Application.Abstractions.Persistence;

namespace OpenTrpg.Core.Application.Catalog;

/// <summary>Every race of the catalog, ordered by name.</summary>
public sealed class ListRacesHandler(ICatalogRepository catalog)
{
    public async Task<IReadOnlyList<RaceSummaryDto>> HandleAsync(CancellationToken cancellationToken = default)
    {
        // Subraces by race from the subraces themselves: content packs add subraces to races of the SRD.
        var subraces = (await catalog.ListAllSubracesAsync(cancellationToken)).ToLookup(s => s.RaceIndex, StringComparer.Ordinal);
        return (await catalog.ListRacesAsync(cancellationToken))
            .Select(r => new RaceSummaryDto(
                r.Index,
                r.Name,
                r.Speed,
                r.Size,
                CatalogJson.AbilityBonuses(r.AbilityBonusesJson),
                r.SubraceIndexes.Concat(subraces[r.Index].Select(s => s.Index)).Distinct(StringComparer.Ordinal).ToList(),
                r.Source))
            .ToList();
    }
}
