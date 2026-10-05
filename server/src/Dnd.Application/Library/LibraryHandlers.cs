using Dnd.Application.Abstractions;
using Dnd.Application.Abstractions.Persistence;
using Dnd.Application.Common;
using Dnd.Application.Files;
using Dnd.Domain.Campaigns;
using Dnd.Domain.Files;
using Dnd.Domain.Library;

namespace Dnd.Application.Library;

public static class LibraryErrors
{
    public static AppException DocumentNotFound() => AppException.NotFound("Documento no encontrado.");
}

/// <summary>Builds the DTOs of library documents (they need the metadata of their files).</summary>
public sealed class LibraryDocumentMapper(IFileRepository files)
{
    public async Task<IReadOnlyList<LibraryDocumentDto>> ToDtosAsync(IReadOnlyList<(LibraryDocument Document, string? Note)> documents, CancellationToken cancellationToken)
    {
        var byId = (await files.ListByIdsAsync(documents.Select(d => d.Document.FileId).Distinct().ToList(), cancellationToken)).ToDictionary(f => f.Id);
        return documents
            .Where(d => byId.ContainsKey(d.Document.FileId))
            .Select(d => LibraryDocumentDto.From(d.Document, byId[d.Document.FileId], d.Note))
            .ToList();
    }

    public async Task<LibraryDocumentDto> ToDtoAsync(LibraryDocument document, string? note, CancellationToken cancellationToken) =>
        (await ToDtosAsync([(document, note)], cancellationToken)).Single();
}

/// <summary>The library of the instance (any authenticated user), by title.</summary>
public sealed class ListLibraryHandler(ILibraryRepository library, LibraryDocumentMapper mapper)
{
    public async Task<IReadOnlyList<LibraryDocumentDto>> HandleAsync(ListLibraryQuery query, CancellationToken cancellationToken = default)
    {
        var search = string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim().ToLowerInvariant();
        var category = string.IsNullOrEmpty(query.Category) ? (LibraryCategory?)null : EnumNames.Parse<LibraryCategory>(query.Category);
        var documents = await library.ListAsync(search, category, cancellationToken);
        return await mapper.ToDtosAsync(documents.Select(d => (d, (string?)null)).ToList(), cancellationToken);
    }
}

/// <summary>An admin publishes an uploaded PDF in the library.</summary>
public sealed class CreateLibraryDocumentHandler(IFileRepository files, ILibraryRepository library, LibraryDocumentMapper mapper, IUnitOfWork unitOfWork, IDateTimeProvider clock)
{
    public async Task<LibraryDocumentDto> HandleAsync(Guid currentUserId, CreateLibraryDocumentRequest request, CancellationToken cancellationToken = default)
    {
        var file = (await files.ListByIdsAsync([request.FileId], cancellationToken)).SingleOrDefault();
        if (file is null || file.Kind != FileKind.LibraryDocument || file.ContentType != FileContent.Pdf)
        {
            throw AppException.Validation("fileId", "El documento debe ser un PDF subido como documento de la biblioteca.");
        }

        var document = LibraryDocument.Create(
            request.Title,
            request.Description,
            EnumNames.Parse<LibraryCategory>(request.Category),
            file.Id,
            isSystem: false,
            currentUserId,
            clock.UtcNow);
        library.Add(document);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await mapper.ToDtoAsync(document, null, cancellationToken);
    }
}

public sealed class UpdateLibraryDocumentHandler(ILibraryRepository library, LibraryDocumentMapper mapper, IUnitOfWork unitOfWork)
{
    public async Task<LibraryDocumentDto> HandleAsync(Guid documentId, UpdateLibraryDocumentRequest request, CancellationToken cancellationToken = default)
    {
        var document = await library.GetAsync(documentId, cancellationToken) ?? throw LibraryErrors.DocumentNotFound();
        document.Update(request.Title, request.Description, request.Category is null ? null : EnumNames.Parse<LibraryCategory>(request.Category));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await mapper.ToDtoAsync(document, null, cancellationToken);
    }
}

/// <summary>Deletes a document, the recommendations that point at it and its file. System documents cannot be deleted (400).</summary>
public sealed class DeleteLibraryDocumentHandler(ILibraryRepository library, FileCleanup cleanup, IUnitOfWork unitOfWork)
{
    public async Task HandleAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        var document = await library.GetAsync(documentId, cancellationToken) ?? throw LibraryErrors.DocumentNotFound();
        document.EnsureCanDelete();
        var fileId = document.FileId;
        library.Remove(document);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await cleanup.ReleaseAsync([fileId], cancellationToken);
    }
}

/// <summary>Documents the DMs of the campaign recommend, with their notes. Any member.</summary>
public sealed class ListCampaignLibraryHandler(ICampaignAccess access, ILibraryRepository library, LibraryDocumentMapper mapper)
{
    public async Task<IReadOnlyList<LibraryDocumentDto>> HandleAsync(Guid currentUserId, Guid campaignId, CancellationToken cancellationToken = default)
    {
        await access.RequireAsync(campaignId, currentUserId, CampaignRole.Player, cancellationToken);
        var recommended = await library.ListRecommendedAsync(campaignId, cancellationToken);
        return await mapper.ToDtosAsync(recommended.Select(r => (r.Document, r.Note)).ToList(), cancellationToken);
    }
}

/// <summary>A DM recommends a document in the campaign (or changes the note of an existing recommendation).</summary>
public sealed class RecommendDocumentHandler(ICampaignAccess access, ILibraryRepository library, IUnitOfWork unitOfWork, IDateTimeProvider clock)
{
    public async Task HandleAsync(Guid currentUserId, Guid campaignId, Guid documentId, RecommendDocumentRequest request, CancellationToken cancellationToken = default)
    {
        await access.RequireAsync(campaignId, currentUserId, CampaignRole.DM, cancellationToken);
        _ = await library.GetAsync(documentId, cancellationToken) ?? throw LibraryErrors.DocumentNotFound();

        var existing = await library.GetRecommendationAsync(campaignId, documentId, cancellationToken);
        if (existing is null)
        {
            library.Add(CampaignDocument.Create(campaignId, documentId, request.Note, clock.UtcNow));
        }
        else
        {
            existing.SetNote(request.Note);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

public sealed class RemoveRecommendationHandler(ICampaignAccess access, ILibraryRepository library, IUnitOfWork unitOfWork)
{
    public async Task HandleAsync(Guid currentUserId, Guid campaignId, Guid documentId, CancellationToken cancellationToken = default)
    {
        await access.RequireAsync(campaignId, currentUserId, CampaignRole.DM, cancellationToken);
        var recommendation = await library.GetRecommendationAsync(campaignId, documentId, cancellationToken)
            ?? throw AppException.NotFound("El documento no está recomendado en esta campaña.");
        library.Remove(recommendation);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
