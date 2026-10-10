using OpenTrpg.Core.Application.Abstractions;
using OpenTrpg.Core.Application.Abstractions.Persistence;
using OpenTrpg.Core.Application.Common;
using OpenTrpg.Core.Application.Files;
using OpenTrpg.Core.Domain.Files;
using OpenTrpg.Core.Domain.Releases;

namespace OpenTrpg.Core.Application.Releases;

public static class ReleaseErrors
{
    public static AppException ReleaseNotFound() => AppException.NotFound("Release no encontrada.");
}

/// <summary>The latest release by build number (what the app compares against); null when none was published.</summary>
public sealed class GetLatestReleaseHandler(IReleaseRepository releases, IFileRepository files)
{
    public async Task<LatestReleaseDto?> HandleAsync(CancellationToken cancellationToken = default)
    {
        var release = await releases.GetLatestAsync(cancellationToken);
        if (release is null)
        {
            return null;
        }

        var file = (await files.ListByIdsAsync([release.FileId], cancellationToken)).SingleOrDefault();
        return file is null ? null : LatestReleaseDto.From(release, file);
    }
}

/// <summary>Opens the APK of a build number for an anonymous download, named <c>dnd-companion-{version}.apk</c>.</summary>
public sealed class DownloadReleaseHandler(IReleaseRepository releases, IFileRepository files, IFileStorage storage)
{
    public async Task<FileDownload> HandleAsync(int buildNumber, CancellationToken cancellationToken = default)
    {
        var release = await releases.GetByBuildNumberAsync(buildNumber, cancellationToken) ?? throw ReleaseErrors.ReleaseNotFound();
        var file = (await files.ListByIdsAsync([release.FileId], cancellationToken)).SingleOrDefault() ?? throw ReleaseErrors.ReleaseNotFound();
        var content = storage.OpenRead(file.StoragePath) ?? throw ReleaseErrors.ReleaseNotFound();
        return new FileDownload(content, file.ContentType, ReleaseUrls.FileNameFor(release.Version), $"\"{file.Sha256}\"", file.CreatedAt);
    }
}

public sealed class ListReleasesHandler(IReleaseRepository releases, IFileRepository files)
{
    public async Task<IReadOnlyList<ReleaseDto>> HandleAsync(CancellationToken cancellationToken = default)
    {
        var list = await releases.ListAsync(cancellationToken);
        var byId = (await files.ListByIdsAsync(list.Select(r => r.FileId).Distinct().ToList(), cancellationToken)).ToDictionary(f => f.Id);
        return list.Where(r => byId.ContainsKey(r.FileId)).Select(r => ReleaseDto.From(r, byId[r.FileId])).ToList();
    }
}

/// <summary>
/// An admin publishes a release. The APK comes either in the same request (<paramref name="content"/>,
/// stored as an AppRelease file) or as the id of a file already uploaded. Order of checks: metadata
/// (400), repeated version or build number (409), then the file (400/413).
/// </summary>
public sealed class PublishReleaseHandler(
    IReleaseRepository releases,
    IFileRepository files,
    UploadFileHandler uploads,
    FileCleanup cleanup,
    IUnitOfWork unitOfWork,
    IDateTimeProvider clock)
{
    /// <param name="request">Already validated with <see cref="PublishReleaseRequestValidator"/>.</param>
    /// <param name="content">Seekable stream with the APK, or null when <see cref="PublishReleaseRequest.FileId"/> is used.</param>
    public async Task<ReleaseDto> HandleAsync(
        Guid currentUserId,
        PublishReleaseRequest request,
        Stream? content,
        string? fileName,
        CancellationToken cancellationToken = default)
    {
        if (content is null && request.FileId is null)
        {
            throw AppException.Validation("file", "Adjunta el APK (campo file) o indica el fileId de un APK ya subido.");
        }

        if (content is not null && request.FileId is not null)
        {
            throw AppException.Validation("fileId", "Indica el APK como fichero adjunto o como fileId, no ambos.");
        }

        var version = request.Version!.Trim();
        var buildNumber = request.BuildNumber!.Value;
        if (await releases.VersionExistsAsync(version, cancellationToken))
        {
            throw AppException.Conflict($"Ya existe una release con la versión {version}.");
        }

        if (await releases.BuildNumberExistsAsync(buildNumber, cancellationToken))
        {
            throw AppException.Conflict($"Ya existe una release con el número de compilación {buildNumber}.");
        }

        var uploaded = content is not null;
        StoredFile file;
        if (content is not null)
        {
            var dto = await uploads.HandleAsync(
                currentUserId,
                new UploadFileCommand(content, fileName ?? "app.apk", nameof(FileKind.AppRelease), null, null, IsAdmin: true),
                cancellationToken);
            file = (await files.ListByIdsAsync([dto.Id], cancellationToken)).Single();
        }
        else
        {
            file = (await files.ListByIdsAsync([request.FileId!.Value], cancellationToken)).SingleOrDefault()
                ?? throw AppException.Validation("fileId", "El fichero no existe.");
            if (file.Kind != FileKind.AppRelease || file.ContentType != FileContent.Apk)
            {
                throw AppException.Validation("fileId", "El fichero debe ser un APK subido como AppRelease.");
            }

            if (await files.IsReferencedAsync(file.Id, cancellationToken))
            {
                throw AppException.Conflict("Ese APK ya está publicado en otra release.");
            }
        }

        try
        {
            var release = AppRelease.Create(version, buildNumber, file.Id, request.Notes, request.IsMandatory ?? false, clock.UtcNow);
            releases.Add(release);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return ReleaseDto.From(release, file);
        }
        catch when (uploaded)
        {
            // Do not leave the APK just stored without a release.
            await cleanup.ReleaseAsync([file.Id], CancellationToken.None);
            throw;
        }
    }
}

/// <summary>Deletes a release and its APK.</summary>
public sealed class DeleteReleaseHandler(IReleaseRepository releases, FileCleanup cleanup, IUnitOfWork unitOfWork)
{
    public async Task HandleAsync(Guid releaseId, CancellationToken cancellationToken = default)
    {
        var release = await releases.GetAsync(releaseId, cancellationToken) ?? throw ReleaseErrors.ReleaseNotFound();
        var fileId = release.FileId;
        releases.Remove(release);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await cleanup.ReleaseAsync([fileId], cancellationToken);
    }
}

/// <summary>Counts of users, campaigns, characters, scheduled sessions and stored files, plus the latest release.</summary>
public sealed class GetAdminStatsHandler(
    IInstanceStatsRepository stats,
    IReleaseRepository releases,
    IDateTimeProvider clock)
{
    public async Task<AdminStatsDto> HandleAsync(string apiVersion, CancellationToken cancellationToken = default)
    {
        var counts = await stats.GetCountsAsync(clock.UtcNow, cancellationToken);
        var latest = await releases.GetLatestAsync(cancellationToken);
        return new AdminStatsDto(
            apiVersion,
            new StatsUsersDto(counts.UsersTotal, counts.UsersActive),
            counts.Campaigns,
            counts.Characters,
            counts.ScheduledSessions,
            new StatsFilesDto(counts.Files, counts.FilesBytes),
            latest is null ? null : new StatsLatestReleaseDto(latest.Version, latest.BuildNumber, latest.PublishedAt));
    }
}
