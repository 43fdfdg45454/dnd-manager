using Dnd.Application.Abstractions.Persistence;
using Dnd.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace Dnd.Infrastructure.Persistence.Repositories;

internal sealed class InstanceSettingsRepository(AppDbContext db) : IInstanceSettingsRepository
{
    public Task<InstanceSetting?> FindAsync(string key, CancellationToken cancellationToken = default) =>
        db.InstanceSettings.AsNoTracking().FirstOrDefaultAsync(x => x.Key == key, cancellationToken);

    public async Task UpsertAsync(string key, string value, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        var existing = await db.InstanceSettings.FirstOrDefaultAsync(x => x.Key == key, cancellationToken);
        if (existing is null)
        {
            db.InstanceSettings.Add(InstanceSetting.Create(key, value, now));
        }
        else
        {
            existing.Update(value, now);
        }
    }
}
