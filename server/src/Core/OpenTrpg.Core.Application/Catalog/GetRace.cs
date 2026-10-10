using OpenTrpg.Core.Application.Abstractions.Persistence;
using OpenTrpg.Core.Domain.Catalog;

namespace OpenTrpg.Core.Application.Catalog;

/// <summary>Race detail with its traits and its subraces (each with its own traits).</summary>
public sealed class GetRaceHandler(ICatalogRepository catalog)
{
    public async Task<RaceDetailDto> HandleAsync(string index, CancellationToken cancellationToken = default)
    {
        var race = await catalog.GetRaceAsync(index, cancellationToken) ?? throw CatalogErrors.RaceNotFound();
        var subraces = await catalog.ListSubracesAsync(race.Index, cancellationToken);
        var extensions = await catalog.ListRaceExtensionsAsync([race.Index], cancellationToken);
        var raceTraits = race.TraitIndexes.Concat(extensions.SelectMany(e => e.TraitIndexes)).Distinct().ToList();
        var grants = extensions.Aggregate(race.Grants, (all, extension) => all.Merge(extension.Grants));
        var heightWeight = extensions.Select(e => e.HeightWeight).LastOrDefault(t => t is not null) ?? race.HeightWeight;
        var traitIndexes = raceTraits.Concat(subraces.SelectMany(s => s.TraitIndexes)).Distinct().ToList();
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
            Traits(raceTraits, traits),
            subraces
                .Select(s => new SubraceDto(s.Index, s.Name, s.Description, CatalogJson.AbilityBonuses(s.AbilityBonusesJson), Traits(s.TraitIndexes, traits))
                {
                    Choices = RaceChoicesDto.From(s.Choices),
                    Resistances = s.Resistances,
                    Speed = s.Speed,
                    Grants = OriginGrantsDto.From(s.Grants),
                    HeightWeight = HeightWeightDto.From(s.HeightWeight),
                    Source = s.Source,
                })
                .ToList(),
            race.Source)
        {
            Choices = RaceChoicesDto.From(race.Choices),
            Resistances = race.Resistances,
            Grants = OriginGrantsDto.From(grants),
            HeightWeight = HeightWeightDto.From(heightWeight),
        };
    }

    private static List<TraitDto> Traits(IEnumerable<string> indexes, IReadOnlyDictionary<string, TraitDefinition> traits) =>
        indexes.Select(i => traits.GetValueOrDefault(i)).OfType<TraitDefinition>().Select(TraitDto.From).ToList();
}
