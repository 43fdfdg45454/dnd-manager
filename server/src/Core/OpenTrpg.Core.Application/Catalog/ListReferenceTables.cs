using OpenTrpg.Core.Application.Abstractions.Persistence;

namespace OpenTrpg.Core.Application.Catalog;

/// <summary>Every condition, ordered by name.</summary>
public sealed class ListConditionsHandler(ICatalogRepository catalog)
{
    public async Task<IReadOnlyList<ConditionDto>> HandleAsync(CancellationToken cancellationToken = default) =>
        (await catalog.ListConditionsAsync(cancellationToken)).Select(ConditionDto.From).ToList();
}

/// <summary>Every skill, ordered by name.</summary>
public sealed class ListSkillsHandler(ICatalogRepository catalog)
{
    public async Task<IReadOnlyList<SkillDto>> HandleAsync(CancellationToken cancellationToken = default) =>
        (await catalog.ListSkillsAsync(cancellationToken)).Select(SkillDto.From).ToList();
}

/// <summary>Every background, ordered by name, with its starting equipment resolved against the catalog.</summary>
public sealed class ListBackgroundsHandler(ICatalogRepository catalog)
{
    public async Task<IReadOnlyList<BackgroundDto>> HandleAsync(CancellationToken cancellationToken = default)
    {
        var backgrounds = (await catalog.ListBackgroundsAsync(cancellationToken))
            .Select(b => (Definition: b, Equipment: b.StartingEquipment))
            .ToList();
        var resolve = await StartingEquipmentResolver.PrepareAsync(catalog, backgrounds.Select(b => b.Equipment), cancellationToken);
        return backgrounds.Select(b => BackgroundDto.From(b.Definition, resolve(b.Equipment))).ToList();
    }
}
