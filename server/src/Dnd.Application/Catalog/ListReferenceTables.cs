using Dnd.Application.Abstractions.Persistence;

namespace Dnd.Application.Catalog;

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

/// <summary>Every background, ordered by name.</summary>
public sealed class ListBackgroundsHandler(ICatalogRepository catalog)
{
    public async Task<IReadOnlyList<BackgroundDto>> HandleAsync(CancellationToken cancellationToken = default) =>
        (await catalog.ListBackgroundsAsync(cancellationToken)).Select(BackgroundDto.From).ToList();
}
