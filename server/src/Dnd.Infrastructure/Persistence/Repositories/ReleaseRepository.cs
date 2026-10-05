using Dnd.Application.Abstractions.Persistence;
using Dnd.Domain.Releases;
using Microsoft.EntityFrameworkCore;

namespace Dnd.Infrastructure.Persistence.Repositories;

internal sealed class ReleaseRepository(AppDbContext db) : IReleaseRepository
{
    public Task<AppRelease?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        db.AppReleases.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<AppRelease?> GetLatestAsync(CancellationToken cancellationToken = default) =>
        db.AppReleases.AsNoTracking().OrderByDescending(x => x.BuildNumber).FirstOrDefaultAsync(cancellationToken);

    public Task<AppRelease?> GetByBuildNumberAsync(int buildNumber, CancellationToken cancellationToken = default) =>
        db.AppReleases.AsNoTracking().FirstOrDefaultAsync(x => x.BuildNumber == buildNumber, cancellationToken);

    public async Task<IReadOnlyList<AppRelease>> ListAsync(CancellationToken cancellationToken = default) =>
        await db.AppReleases.AsNoTracking().OrderByDescending(x => x.BuildNumber).ToListAsync(cancellationToken);

    public Task<bool> VersionExistsAsync(string version, CancellationToken cancellationToken = default) =>
        db.AppReleases.AnyAsync(x => x.Version == version, cancellationToken);

    public Task<bool> BuildNumberExistsAsync(int buildNumber, CancellationToken cancellationToken = default) =>
        db.AppReleases.AnyAsync(x => x.BuildNumber == buildNumber, cancellationToken);

    public void Add(AppRelease release) => db.AppReleases.Add(release);

    public void Remove(AppRelease release) => db.AppReleases.Remove(release);
}
