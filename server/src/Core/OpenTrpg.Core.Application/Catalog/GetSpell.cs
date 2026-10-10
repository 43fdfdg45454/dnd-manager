using OpenTrpg.Core.Application.Abstractions.Persistence;

namespace OpenTrpg.Core.Application.Catalog;

public sealed class GetSpellHandler(ICatalogRepository catalog)
{
    public async Task<SpellDetailDto> HandleAsync(string index, CancellationToken cancellationToken = default)
    {
        var s = await catalog.GetSpellAsync(index, cancellationToken) ?? throw CatalogErrors.SpellNotFound();
        var expansions = SpellExpansionDto.Lookup(await catalog.ListSubclassesWithExpandedSpellsAsync(cancellationToken));
        return new SpellDetailDto(
            s.Index,
            s.Name,
            s.Level,
            s.School,
            s.CastingTime,
            s.Range,
            s.Components,
            s.Material,
            s.Duration,
            s.Concentration,
            s.Ritual,
            s.Description,
            s.HigherLevel,
            s.ClassIndexes,
            s.SubclassIndexes,
            s.AttackType,
            CatalogJson.SpellDamage(s.DamageJson),
            CatalogJson.LevelMap(s.HealJson),
            s.DcAbility,
            s.Source,
            s.Category.ToString(),
            expansions[s.Index].ToList());
    }
}
