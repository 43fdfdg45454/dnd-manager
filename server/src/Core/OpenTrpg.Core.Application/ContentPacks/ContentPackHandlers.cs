using OpenTrpg.Core.Application.Abstractions;
using OpenTrpg.Core.Application.Common;

namespace OpenTrpg.Core.Application.ContentPacks;

/// <summary>Imported content packs (admin).</summary>
public sealed class ListContentPacksHandler(IContentPackImporter importer)
{
    public Task<IReadOnlyList<ContentPackDto>> HandleAsync(CancellationToken cancellationToken = default) =>
        importer.ListAsync(cancellationToken);
}

/// <summary>Imports or replaces a content pack (admin). Throws <see cref="ContentPackInvalidException"/> when it is not valid.</summary>
public sealed class ImportContentPackHandler(IContentPackImporter importer)
{
    public Task<ContentPackImportResultDto> HandleAsync(Stream json, CancellationToken cancellationToken = default) =>
        importer.ImportAsync(json, cancellationToken);
}

/// <summary>Deletes a content pack (admin). Characters keep their indexes and show the content as missing.</summary>
public sealed class DeleteContentPackHandler(IContentPackImporter importer)
{
    public async Task HandleAsync(string id, CancellationToken cancellationToken = default)
    {
        if (!await importer.DeleteAsync(id, cancellationToken))
        {
            throw AppException.NotFound("El paquete de contenido no existe.");
        }
    }
}
