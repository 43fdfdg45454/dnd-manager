using OpenTrpg.Core.Domain.Releases;

namespace OpenTrpg.Core.Application.Abstractions.Persistence;

public interface IReleaseRepository
{
    /// <summary>Tracked release, or null.</summary>
    Task<AppRelease?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Read-only release with the highest build number, or null when none was published.</summary>
    Task<AppRelease?> GetLatestAsync(CancellationToken cancellationToken = default);

    /// <summary>Read-only release with that build number, or null.</summary>
    Task<AppRelease?> GetByBuildNumberAsync(int buildNumber, CancellationToken cancellationToken = default);

    /// <summary>Read-only releases, newest build number first.</summary>
    Task<IReadOnlyList<AppRelease>> ListAsync(CancellationToken cancellationToken = default);

    Task<bool> VersionExistsAsync(string version, CancellationToken cancellationToken = default);

    Task<bool> BuildNumberExistsAsync(int buildNumber, CancellationToken cancellationToken = default);

    void Add(AppRelease release);

    void Remove(AppRelease release);
}
