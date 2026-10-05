using Dnd.Application.Abstractions;
using Dnd.Application.Abstractions.Persistence;
using Dnd.Domain.Common;

namespace Dnd.Api.Hosting;

/// <summary>
/// The last public origin seen in a request, persisted in <c>InstanceSettings</c> so the background services
/// (reminders) and the next boot know it. It is cached in memory: the database is read once and written only
/// when the origin changes.
/// </summary>
public sealed class PublicOriginStore(IServiceScopeFactory scopes, ILogger<PublicOriginStore> logger)
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private volatile string? _cached;
    private volatile bool _loaded;

    /// <summary>The last origin seen, or null when none was recorded yet.</summary>
    public async Task<string?> GetAsync(CancellationToken cancellationToken = default)
    {
        if (_loaded)
        {
            return _cached;
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            await EnsureLoadedAsync(cancellationToken);
            return _cached;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Records <paramref name="origin"/> as the last one seen. Does nothing when it is the one already stored.</summary>
    public async Task RememberAsync(string origin, CancellationToken cancellationToken = default)
    {
        if (_loaded && string.Equals(_cached, origin, StringComparison.Ordinal))
        {
            return;
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            await EnsureLoadedAsync(cancellationToken);
            if (string.Equals(_cached, origin, StringComparison.Ordinal))
            {
                return;
            }

            await using var scope = scopes.CreateAsyncScope();
            var settings = scope.ServiceProvider.GetRequiredService<IInstanceSettingsRepository>();
            var clock = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>();
            await settings.UpsertAsync(InstanceSetting.PublicOriginKey, origin, clock.UtcNow, cancellationToken);
            await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(cancellationToken);

            logger.LogInformation("Public origin of the instance is now {Origin}", origin);
            _cached = origin;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task EnsureLoadedAsync(CancellationToken cancellationToken)
    {
        if (_loaded)
        {
            return;
        }

        await using var scope = scopes.CreateAsyncScope();
        var settings = scope.ServiceProvider.GetRequiredService<IInstanceSettingsRepository>();
        _cached = (await settings.FindAsync(InstanceSetting.PublicOriginKey, cancellationToken))?.Value;
        _loaded = true;
    }
}
