using Dnd.Domain.Common;

namespace Dnd.Application.Abstractions.Persistence;

public interface IInstanceSettingsRepository
{
    Task<InstanceSetting?> FindAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>Creates or updates the setting. Persisted by the unit of work.</summary>
    Task UpsertAsync(string key, string value, DateTimeOffset now, CancellationToken cancellationToken = default);
}
