using Dnd.Application.Abstractions.Persistence;
using Dnd.Domain.Catalog;

namespace Dnd.Application.Catalog;

/// <summary>Race detail with its traits and its subraces (each with its own traits).</summary>
public sealed class GetRaceHandler(ICatalogRepository catalog)
{
    public async Task<RaceDetailDto> HandleAsync(string index, CancellationToken cancellationToken = default)
    {
        var race = await catalog.GetRaceAsync(index, cancellationToken) ?? throw CatalogErrors.RaceNotFound();
        var subraces = await catalog.ListSubracesAsync(race.Index, cancellationToken);
        var traitIndexes = race.TraitIndexes.Concat(subraces.SelectMany(s => s.TraitIndexes)).Distinct().ToList();
        var traits = (await catalog.ListTraitsAsync(traitIndexes, cancellationToken)).ToDictionary(t => t.Index);

        return new RaceDetailDto(
            race.Index,
            race.Name,
            race.Speed,
            race.Size,
            race.SizeDescription,
            CatalogJson.AbilityBonuses(race.AbilityBonusesJson),
            race.Languages,
            race.Age,
            race.Alignment,
            Traits(race.TraitIndexes, traits),
            subraces
                .Select(s => new SubraceDto(s.Index, s.Name, s.Description, CatalogJson.AbilityBonuses(s.AbilityBonusesJson), Traits(s.TraitIndexes, traits)))
                .ToList());
    }

    private static List<TraitDto> Traits(IEnumerable<string> indexes, IReadOnlyDictionary<string, TraitDefinition> traits) =>
        indexes.Select(i => traits.GetValueOrDefault(i)).OfType<TraitDefinition>().Select(TraitDto.From).ToList();
}
