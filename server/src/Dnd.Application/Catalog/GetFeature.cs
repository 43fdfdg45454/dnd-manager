using Dnd.Application.Abstractions.Persistence;

namespace Dnd.Application.Catalog;

public sealed class GetFeatureHandler(ICatalogRepository catalog)
{
    public async Task<FeatureDto> HandleAsync(string index, CancellationToken cancellationToken = default) =>
        FeatureDto.From(await catalog.GetFeatureAsync(index, cancellationToken) ?? throw CatalogErrors.FeatureNotFound());
}
