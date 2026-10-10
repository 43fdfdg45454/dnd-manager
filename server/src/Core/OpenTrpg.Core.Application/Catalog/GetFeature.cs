using OpenTrpg.Core.Application.Abstractions.Persistence;

namespace OpenTrpg.Core.Application.Catalog;

public sealed class GetFeatureHandler(ICatalogRepository catalog)
{
    public async Task<FeatureDto> HandleAsync(string index, CancellationToken cancellationToken = default) =>
        FeatureDto.From(await catalog.GetFeatureAsync(index, cancellationToken) ?? throw CatalogErrors.FeatureNotFound());
}
