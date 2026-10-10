using OpenTrpg.Core.Application.Abstractions;
using OpenTrpg.Core.Application.Abstractions.Persistence;
using OpenTrpg.Core.Application.Common;
using OpenTrpg.Core.Application.Files;
using OpenTrpg.Core.Domain.Campaigns;
using OpenTrpg.Core.Domain.Common;
using OpenTrpg.Core.Domain.Files;
using OpenTrpg.Core.Domain.Lore;

namespace OpenTrpg.Core.Application.Lore;

public static class LoreErrors
{
    /// <summary>Also used for users who are not members and for DM-only entries asked by players.</summary>
    public static AppException EntryNotFound() => AppException.NotFound("Entrada no encontrada.");
}

/// <summary>A lore entry loaded for a use case, with the role of the acting user in its campaign.</summary>
public sealed record LoadedLoreEntry(LoreEntry Entry, CampaignRole Role)
{
    public bool IsDm => Role.IsAtLeast(CampaignRole.DM);
}

/// <summary>Loads lore entries (tracked, with attachments), applies visibility and builds DTOs.</summary>
public sealed class LoreLoader(ILoreRepository lore, ICampaignAccess access, IFileRepository files, CampaignFileGuard fileGuard)
{
    /// <summary>404 when the entry does not exist, the actor is not a member of its campaign or the entry is DM-only and the actor a player.</summary>
    public async Task<LoadedLoreEntry> LoadAsync(Guid entryId, Guid actorUserId, CancellationToken cancellationToken)
    {
        var entry = await lore.GetWithAttachmentsAsync(entryId, cancellationToken) ?? throw LoreErrors.EntryNotFound();
        var role = await access.GetRoleAsync(entry.CampaignId, actorUserId, cancellationToken) ?? throw LoreErrors.EntryNotFound();
        var loaded = new LoadedLoreEntry(entry, role);
        return !loaded.IsDm && entry.Visibility == ContentVisibility.DmOnly ? throw LoreErrors.EntryNotFound() : loaded;
    }

    /// <summary>Like <see cref="LoadAsync"/> but only for DMs (403 for players).</summary>
    public async Task<LoreEntry> LoadForDmAsync(Guid entryId, Guid actorUserId, CancellationToken cancellationToken)
    {
        var loaded = await LoadAsync(entryId, actorUserId, cancellationToken);
        return loaded.IsDm ? loaded.Entry : throw AppException.Forbidden("Solo un DM puede editar el lore.");
    }

    public async Task<LoreEntryDto> ToDtoAsync(LoreEntry entry, CancellationToken cancellationToken)
    {
        var fileIds = entry.Attachments.Select(a => a.FileId).ToList();
        var byId = (await files.ListByIdsAsync(fileIds, cancellationToken)).ToDictionary(f => f.Id);
        var attachments = entry.Attachments
            .Where(a => byId.ContainsKey(a.FileId))
            .OrderBy(a => a.CreatedAt)
            .ThenBy(a => a.Id)
            .Select(a => LoreAttachmentDto.From(a, byId[a.FileId]))
            .ToList();

        return new LoreEntryDto(
            entry.Id,
            entry.CampaignId,
            entry.Title,
            entry.Slug,
            entry.Category.ToString(),
            entry.ContentMarkdown,
            entry.Visibility.ToString(),
            entry.ParentId,
            entry.SortOrder,
            entry.CoverFileId,
            FileUrls.For(entry.CoverFileId),
            entry.CreatedByUserId,
            entry.CreatedAt,
            entry.UpdatedAt,
            attachments);
    }

    /// <summary>The parent must be another entry of the campaign (400 otherwise).</summary>
    public async Task EnsureParentAsync(Guid campaignId, Guid parentId, CancellationToken cancellationToken)
    {
        if (await lore.FindInCampaignAsync(campaignId, parentId, cancellationToken) is null)
        {
            throw AppException.Validation("parentId", "La entrada padre no existe en esta campaña.");
        }
    }

    /// <summary>The cover must be an image uploaded as lore attachment in the campaign (400 otherwise).</summary>
    public Task EnsureCoverAsync(Guid campaignId, Guid fileId, CancellationToken cancellationToken) =>
        fileGuard.RequireAsync(campaignId, fileId, FileKind.LoreAttachment, "coverFileId", "La portada debe ser una imagen subida a esta campaña.", imageOnly: true, cancellationToken);
}

/// <summary>Entries of the campaign as a flat list. Players only get the ones visible to them.</summary>
public sealed class ListLoreHandler(ICampaignAccess access, ILoreRepository lore)
{
    public async Task<IReadOnlyList<LoreSummaryDto>> HandleAsync(Guid currentUserId, Guid campaignId, ListLoreQuery query, CancellationToken cancellationToken = default)
    {
        var role = await access.RequireAsync(campaignId, currentUserId, CampaignRole.Player, cancellationToken);
        var search = string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim().ToLowerInvariant();
        var filter = new LoreFilter(
            PlayersOnly: !role.IsAtLeast(CampaignRole.DM),
            Category: string.IsNullOrEmpty(query.Category) ? null : EnumNames.Parse<LoreCategory>(query.Category),
            Search: search,
            ParentId: query.ParentId);

        var entries = await lore.ListByCampaignAsync(campaignId, filter, cancellationToken);
        return entries.Select(LoreSummaryDto.From).ToList();
    }
}

/// <summary>A DM creates an entry; its slug comes from the title (suffix -2, -3... when taken).</summary>
public sealed class CreateLoreHandler(ICampaignAccess access, ILoreRepository lore, LoreLoader loader, IUnitOfWork unitOfWork, IDateTimeProvider clock)
{
    public async Task<LoreEntryDto> HandleAsync(Guid currentUserId, Guid campaignId, CreateLoreRequest request, CancellationToken cancellationToken = default)
    {
        await access.RequireAsync(campaignId, currentUserId, CampaignRole.DM, cancellationToken);

        if (request.ParentId is { } parentId)
        {
            await loader.EnsureParentAsync(campaignId, parentId, cancellationToken);
        }

        if (request.CoverFileId is { } coverId)
        {
            await loader.EnsureCoverAsync(campaignId, coverId, cancellationToken);
        }

        var slug = await UniqueSlugAsync(campaignId, request.Title, cancellationToken);
        var entry = LoreEntry.Create(
            campaignId,
            request.Title,
            slug,
            EnumNames.Parse<LoreCategory>(request.Category),
            request.ContentMarkdown,
            EnumNames.Parse<ContentVisibility>(request.Visibility),
            request.ParentId,
            request.CoverFileId,
            currentUserId,
            clock.UtcNow);
        lore.Add(entry);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await loader.ToDtoAsync(entry, cancellationToken);
    }

    private async Task<string> UniqueSlugAsync(Guid campaignId, string title, CancellationToken cancellationToken)
    {
        var baseSlug = LoreSlug.From(title);
        for (var attempt = 1; ; attempt++)
        {
            var slug = LoreSlug.Attempt(baseSlug, attempt);
            if (!await lore.SlugExistsAsync(campaignId, slug, cancellationToken))
            {
                return slug;
            }
        }
    }
}

/// <summary>An entry with its attachments. Players get 404 for DM-only entries.</summary>
public sealed class GetLoreHandler(LoreLoader loader)
{
    public async Task<LoreEntryDto> HandleAsync(Guid currentUserId, Guid entryId, CancellationToken cancellationToken = default)
    {
        var loaded = await loader.LoadAsync(entryId, currentUserId, cancellationToken);
        return await loader.ToDtoAsync(loaded.Entry, cancellationToken);
    }
}

public sealed class UpdateLoreHandler(LoreLoader loader, ILoreRepository lore, FileCleanup cleanup, IUnitOfWork unitOfWork, IDateTimeProvider clock)
{
    public async Task<LoreEntryDto> HandleAsync(Guid currentUserId, Guid entryId, UpdateLoreRequest request, CancellationToken cancellationToken = default)
    {
        var entry = await loader.LoadForDmAsync(entryId, currentUserId, cancellationToken);
        var now = clock.UtcNow;

        if (request.ParentId.IsSet && request.ParentId.Value is { } parentId)
        {
            await loader.EnsureParentAsync(entry.CampaignId, parentId, cancellationToken);
            await EnsureNoCycleAsync(entry, parentId, cancellationToken);
        }

        if (request.CoverFileId.IsSet && request.CoverFileId.Value is { } coverId)
        {
            await loader.EnsureCoverAsync(entry.CampaignId, coverId, cancellationToken);
        }

        var previousCover = entry.CoverFileId;
        entry.Update(
            request.Title,
            request.Category is null ? null : EnumNames.Parse<LoreCategory>(request.Category),
            request.ContentMarkdown,
            request.Visibility is null ? null : EnumNames.Parse<ContentVisibility>(request.Visibility),
            request.SortOrder,
            now);
        if (request.ParentId.IsSet)
        {
            entry.SetParent(request.ParentId.Value, now);
        }

        if (request.CoverFileId.IsSet)
        {
            entry.SetCover(request.CoverFileId.Value, now);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        if (previousCover != entry.CoverFileId)
        {
            await cleanup.ReleaseAsync([previousCover], cancellationToken);
        }

        return await loader.ToDtoAsync(entry, cancellationToken);
    }

    /// <summary>The new parent cannot be the entry itself or one of its descendants (400).</summary>
    private async Task EnsureNoCycleAsync(LoreEntry entry, Guid newParentId, CancellationToken cancellationToken)
    {
        var links = await lore.ListParentLinksAsync(entry.CampaignId, cancellationToken);
        Guid? current = newParentId;
        var steps = 0;
        while (current is { } id && steps++ <= links.Count)
        {
            if (id == entry.Id)
            {
                throw AppException.Validation("parentId", "Una entrada no puede colgar de sí misma ni de una de sus descendientes.");
            }

            current = links.GetValueOrDefault(id);
        }
    }
}

/// <summary>Deletes the entry and its attachments; its children become root entries.</summary>
public sealed class DeleteLoreHandler(LoreLoader loader, ILoreRepository lore, FileCleanup cleanup, IUnitOfWork unitOfWork)
{
    public async Task HandleAsync(Guid currentUserId, Guid entryId, CancellationToken cancellationToken = default)
    {
        var entry = await loader.LoadForDmAsync(entryId, currentUserId, cancellationToken);
        var fileIds = entry.Attachments.Select(a => (Guid?)a.FileId).Append(entry.CoverFileId).ToList();

        lore.Remove(entry);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await cleanup.ReleaseAsync(fileIds, cancellationToken);
    }
}

public sealed class AddLoreAttachmentHandler(LoreLoader loader, CampaignFileGuard fileGuard, IUnitOfWork unitOfWork, IDateTimeProvider clock)
{
    public async Task<LoreAttachmentDto> HandleAsync(Guid currentUserId, Guid entryId, AddLoreAttachmentRequest request, CancellationToken cancellationToken = default)
    {
        var entry = await loader.LoadForDmAsync(entryId, currentUserId, cancellationToken);
        var file = await fileGuard.RequireAsync(
            entry.CampaignId, request.FileId, FileKind.LoreAttachment, "fileId", "El adjunto debe ser un fichero subido a esta campaña como adjunto de lore.", imageOnly: false, cancellationToken);

        var attachment = entry.AddAttachment(file.Id, request.Caption, clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return LoreAttachmentDto.From(attachment, file);
    }
}

public sealed class DeleteLoreAttachmentHandler(LoreLoader loader, FileCleanup cleanup, IUnitOfWork unitOfWork, IDateTimeProvider clock)
{
    public async Task HandleAsync(Guid currentUserId, Guid entryId, Guid attachmentId, CancellationToken cancellationToken = default)
    {
        var entry = await loader.LoadForDmAsync(entryId, currentUserId, cancellationToken);
        var fileId = entry.FindAttachment(attachmentId).FileId;
        entry.RemoveAttachment(attachmentId, clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await cleanup.ReleaseAsync([fileId], cancellationToken);
    }
}
