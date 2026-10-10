using OpenTrpg.Core.Domain.Files;
using OpenTrpg.Core.Domain.Releases;

namespace OpenTrpg.Core.Application.Releases;

/// <summary>Where the app downloads the APK of a release from (anonymous).</summary>
public static class ReleaseUrls
{
    public static string DownloadFor(int buildNumber) => $"/api/v1/app/download/{buildNumber}";

    /// <summary>Name the APK is offered under, for example <c>opentrpg-1.2.0.apk</c>.</summary>
    public static string FileNameFor(string version) => $"opentrpg-{version}.apk";
}

/// <summary>What the app needs to decide whether to offer an update (<c>GET /api/v1/app/latest</c>).</summary>
/// <param name="Notes">Release notes; an empty string when there are none.</param>
/// <param name="DownloadUrl">Relative URL of the anonymous APK download.</param>
public sealed record LatestReleaseDto(
    string Version,
    int BuildNumber,
    string Notes,
    bool IsMandatory,
    string DownloadUrl,
    long SizeBytes,
    DateTimeOffset PublishedAt)
{
    public static LatestReleaseDto From(AppRelease release, StoredFile file) => new(
        release.Version,
        release.BuildNumber,
        release.Notes ?? string.Empty,
        release.IsMandatory,
        ReleaseUrls.DownloadFor(release.BuildNumber),
        file.SizeBytes,
        release.PublishedAt);
}

/// <summary>A release as the administrators see it.</summary>
public sealed record ReleaseDto(
    Guid Id,
    string Version,
    int BuildNumber,
    string Notes,
    bool IsMandatory,
    string DownloadUrl,
    long SizeBytes,
    DateTimeOffset PublishedAt)
{
    public static ReleaseDto From(AppRelease release, StoredFile file) => new(
        release.Id,
        release.Version,
        release.BuildNumber,
        release.Notes ?? string.Empty,
        release.IsMandatory,
        ReleaseUrls.DownloadFor(release.BuildNumber),
        file.SizeBytes,
        release.PublishedAt);
}

public sealed record StatsUsersDto(int Total, int Active);

public sealed record StatsFilesDto(int Count, long TotalBytes);

public sealed record StatsLatestReleaseDto(string Version, int BuildNumber, DateTimeOffset PublishedAt);

/// <summary>Numbers of the instance for the admin dashboard (<c>GET /api/v1/admin/stats</c>).</summary>
/// <param name="ScheduledSessions">Sessions scheduled that have not started yet.</param>
/// <param name="LatestRelease">Null when no APK has been published.</param>
public sealed record AdminStatsDto(
    string ApiVersion,
    StatsUsersDto Users,
    int Campaigns,
    int Characters,
    int ScheduledSessions,
    StatsFilesDto Files,
    StatsLatestReleaseDto? LatestRelease);
