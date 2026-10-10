using OpenTrpg.Core.Application.Abstractions;
using OpenTrpg.Core.Application.Abstractions.Persistence;
using OpenTrpg.Core.Application.Common;
using OpenTrpg.Core.Application.Files;
using OpenTrpg.Core.Domain.Campaigns;
using OpenTrpg.Core.Domain.Common;
using OpenTrpg.Core.Domain.Files;
using OpenTrpg.Core.Domain.Maps;

namespace OpenTrpg.Core.Application.Maps;

public static class MapErrors
{
    /// <summary>Also used for users who are not members and for DM-only maps asked by players.</summary>
    public static AppException MapNotFound() => AppException.NotFound("Mapa no encontrado.");
}

/// <summary>A map loaded for a use case, with the role of the acting user in its campaign.</summary>
public sealed record LoadedMap(Map Map, CampaignRole Role)
{
    public bool IsDm => Role.IsAtLeast(CampaignRole.DM);
}

/// <summary>Loads maps (tracked, with pins), applies visibility and builds DTOs.</summary>
public sealed class MapLoader(IMapRepository maps, ICampaignAccess access, CampaignFileGuard fileGuard, ILoreRepository lore)
{
    /// <summary>404 when the map does not exist, the actor is not a member of its campaign or the map is DM-only and the actor a player.</summary>
    public async Task<LoadedMap> LoadAsync(Guid mapId, Guid actorUserId, CancellationToken cancellationToken)
    {
        var map = await maps.GetWithPinsAsync(mapId, cancellationToken) ?? throw MapErrors.MapNotFound();
        var role = await access.GetRoleAsync(map.CampaignId, actorUserId, cancellationToken) ?? throw MapErrors.MapNotFound();
        var loaded = new LoadedMap(map, role);
        return !loaded.IsDm && map.Visibility == ContentVisibility.DmOnly ? throw MapErrors.MapNotFound() : loaded;
    }

    /// <summary>Like <see cref="LoadAsync"/> but only for DMs (403 for players).</summary>
    public async Task<Map> LoadForDmAsync(Guid mapId, Guid actorUserId, CancellationToken cancellationToken)
    {
        var loaded = await LoadAsync(mapId, actorUserId, cancellationToken);
        return loaded.IsDm ? loaded.Map : throw AppException.Forbidden("Solo un DM puede gestionar los mapas.");
    }

    /// <summary>The image must be a map image (picture) uploaded to the campaign (400 otherwise).</summary>
    public async Task<StoredFile> RequireImageAsync(Guid campaignId, Guid fileId, CancellationToken cancellationToken)
    {
        var file = await fileGuard.RequireAsync(
            campaignId, fileId, FileKind.MapImage, "fileId", "El mapa debe ser una imagen PNG, JPEG o WebP subida a esta campaña.", imageOnly: true, cancellationToken);
        return file.WidthPx is null || file.HeightPx is null
            ? throw AppException.Validation("fileId", "No se pudo leer el tamaño de la imagen.")
            : file;
    }

    public async Task EnsureLoreEntryAsync(Guid campaignId, Guid loreEntryId, CancellationToken cancellationToken)
    {
        if (await lore.FindInCampaignAsync(campaignId, loreEntryId, cancellationToken) is null)
        {
            throw AppException.Validation("loreEntryId", "La entrada de lore no existe en esta campaña.");
        }
    }

    /// <summary>The DTO with the pins the viewer may see.</summary>
    public static MapDto ToDto(Map map, bool viewerIsDm) => new(
        map.Id,
        map.CampaignId,
        map.Name,
        map.FileId,
        FileUrls.For(map.FileId),
        map.WidthPx,
        map.HeightPx,
        map.Visibility.ToString(),
        map.SortOrder,
        map.CreatedAt,
        map.Pins
            .Where(p => viewerIsDm || p.Visibility == ContentVisibility.Players)
            .OrderBy(p => p.CreatedAt)
            .ThenBy(p => p.Id)
            .Select(MapPinDto.From)
            .ToList());
}

/// <summary>Maps of the campaign. Players only get the ones visible to them.</summary>
public sealed class ListMapsHandler(ICampaignAccess access, IMapRepository maps)
{
    public async Task<IReadOnlyList<MapSummaryDto>> HandleAsync(Guid currentUserId, Guid campaignId, CancellationToken cancellationToken = default)
    {
        var role = await access.RequireAsync(campaignId, currentUserId, CampaignRole.Player, cancellationToken);
        var list = await maps.ListByCampaignAsync(campaignId, playersOnly: !role.IsAtLeast(CampaignRole.DM), cancellationToken);
        return list.Select(MapSummaryDto.From).ToList();
    }
}

/// <summary>A DM creates a map from an image uploaded to the campaign; its pixel size is read from the stored image.</summary>
public sealed class CreateMapHandler(ICampaignAccess access, IMapRepository maps, MapLoader loader, IUnitOfWork unitOfWork, IDateTimeProvider clock)
{
    public async Task<MapDto> HandleAsync(Guid currentUserId, Guid campaignId, CreateMapRequest request, CancellationToken cancellationToken = default)
    {
        await access.RequireAsync(campaignId, currentUserId, CampaignRole.DM, cancellationToken);
        var file = await loader.RequireImageAsync(campaignId, request.FileId, cancellationToken);

        var map = Map.Create(
            campaignId,
            request.Name,
            file.Id,
            file.WidthPx!.Value,
            file.HeightPx!.Value,
            EnumNames.Parse<ContentVisibility>(request.Visibility),
            await maps.MaxSortOrderAsync(campaignId, cancellationToken) + 1,
            clock.UtcNow);
        maps.Add(map);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return MapLoader.ToDto(map, viewerIsDm: true);
    }
}

/// <summary>A map with the pins the viewer may see. Players get 404 for DM-only maps.</summary>
public sealed class GetMapHandler(MapLoader loader)
{
    public async Task<MapDto> HandleAsync(Guid currentUserId, Guid mapId, CancellationToken cancellationToken = default)
    {
        var loaded = await loader.LoadAsync(mapId, currentUserId, cancellationToken);
        return MapLoader.ToDto(loaded.Map, loaded.IsDm);
    }
}

public sealed class UpdateMapHandler(MapLoader loader, FileCleanup cleanup, IUnitOfWork unitOfWork, IDateTimeProvider clock)
{
    public async Task<MapDto> HandleAsync(Guid currentUserId, Guid mapId, UpdateMapRequest request, CancellationToken cancellationToken = default)
    {
        var map = await loader.LoadForDmAsync(mapId, currentUserId, cancellationToken);
        var now = clock.UtcNow;

        StoredFile? newImage = null;
        if (request.FileId is { } fileId && fileId != map.FileId)
        {
            newImage = await loader.RequireImageAsync(map.CampaignId, fileId, cancellationToken);
        }

        var previousFileId = map.FileId;
        map.Update(
            request.Name,
            request.Visibility is null ? null : EnumNames.Parse<ContentVisibility>(request.Visibility),
            request.SortOrder,
            now);
        if (newImage is not null)
        {
            map.ReplaceImage(newImage.Id, newImage.WidthPx!.Value, newImage.HeightPx!.Value, now);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        if (newImage is not null)
        {
            await cleanup.ReleaseAsync([previousFileId], cancellationToken);
        }

        return MapLoader.ToDto(map, viewerIsDm: true);
    }
}

/// <summary>Deletes the map, its pins and its image.</summary>
public sealed class DeleteMapHandler(MapLoader loader, IMapRepository maps, FileCleanup cleanup, IUnitOfWork unitOfWork)
{
    public async Task HandleAsync(Guid currentUserId, Guid mapId, CancellationToken cancellationToken = default)
    {
        var map = await loader.LoadForDmAsync(mapId, currentUserId, cancellationToken);
        var fileId = map.FileId;
        maps.Remove(map);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await cleanup.ReleaseAsync([fileId], cancellationToken);
    }
}

public sealed class CreatePinHandler(MapLoader loader, IUnitOfWork unitOfWork, IDateTimeProvider clock)
{
    public async Task<MapPinDto> HandleAsync(Guid currentUserId, Guid mapId, CreatePinRequest request, CancellationToken cancellationToken = default)
    {
        var map = await loader.LoadForDmAsync(mapId, currentUserId, cancellationToken);
        if (request.LoreEntryId is { } loreId)
        {
            await loader.EnsureLoreEntryAsync(map.CampaignId, loreId, cancellationToken);
        }

        var pin = map.AddPin(
            request.X!.Value,
            request.Y!.Value,
            request.Title,
            request.Note,
            request.Icon,
            request.Color,
            request.LoreEntryId,
            EnumNames.Parse<ContentVisibility>(request.Visibility),
            clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return MapPinDto.From(pin);
    }
}

public sealed class UpdatePinHandler(MapLoader loader, IUnitOfWork unitOfWork, IDateTimeProvider clock)
{
    public async Task<MapPinDto> HandleAsync(Guid currentUserId, Guid mapId, Guid pinId, UpdatePinRequest request, CancellationToken cancellationToken = default)
    {
        var map = await loader.LoadForDmAsync(mapId, currentUserId, cancellationToken);
        map.FindPin(pinId);
        if (request.LoreEntryId.IsSet && request.LoreEntryId.Value is { } loreId)
        {
            await loader.EnsureLoreEntryAsync(map.CampaignId, loreId, cancellationToken);
        }

        var pin = map.UpdatePin(
            pinId,
            request.X,
            request.Y,
            request.Title,
            request.Note,
            request.Icon,
            (request.Color.IsSet, request.Color.Value),
            (request.LoreEntryId.IsSet, request.LoreEntryId.Value),
            request.Visibility is null ? null : EnumNames.Parse<ContentVisibility>(request.Visibility),
            clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return MapPinDto.From(pin);
    }
}

public sealed class DeletePinHandler(MapLoader loader, IUnitOfWork unitOfWork, IDateTimeProvider clock)
{
    public async Task HandleAsync(Guid currentUserId, Guid mapId, Guid pinId, CancellationToken cancellationToken = default)
    {
        var map = await loader.LoadForDmAsync(mapId, currentUserId, cancellationToken);
        map.RemovePin(pinId, clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
