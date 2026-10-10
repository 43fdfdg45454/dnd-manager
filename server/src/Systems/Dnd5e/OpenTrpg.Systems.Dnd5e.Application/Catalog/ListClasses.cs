using OpenTrpg.Core.Application.Abstractions.Persistence;
using OpenTrpg.Core.Application;
using OpenTrpg.Systems.Dnd5e.Application;
using OpenTrpg.Systems.Dnd5e.Application.Abstractions.Persistence;

namespace OpenTrpg.Systems.Dnd5e.Application.Catalog;

/// <summary>Every class of the catalog, ordered by name.</summary>
public sealed class ListClassesHandler(ICatalogRepository catalog)
{
    public async Task<IReadOnlyList<ClassSummaryDto>> HandleAsync(CancellationToken cancellationToken = default) =>
        (await catalog.ListClassesAsync(cancellationToken)).Select(ClassSummaryDto.From).ToList();
}
