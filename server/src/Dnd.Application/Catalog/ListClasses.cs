using Dnd.Application.Abstractions.Persistence;

namespace Dnd.Application.Catalog;

/// <summary>Every class of the catalog, ordered by name.</summary>
public sealed class ListClassesHandler(ICatalogRepository catalog)
{
    public async Task<IReadOnlyList<ClassSummaryDto>> HandleAsync(CancellationToken cancellationToken = default) =>
        (await catalog.ListClassesAsync(cancellationToken)).Select(ClassSummaryDto.From).ToList();
}
