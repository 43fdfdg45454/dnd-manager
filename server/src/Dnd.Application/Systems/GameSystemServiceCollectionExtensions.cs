using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Dnd.Application.Systems;

public static class GameSystemServiceCollectionExtensions
{
    /// <summary>Registers a game system (singleton) and the <see cref="IGameSystemRegistry"/> that lists it.</summary>
    public static IServiceCollection AddGameSystem<TSystem>(this IServiceCollection services)
        where TSystem : class, IGameSystem
    {
        services.TryAddSingleton<TSystem>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IGameSystem, TSystem>(sp => sp.GetRequiredService<TSystem>()));
        services.TryAddSingleton<IGameSystemRegistry, GameSystemRegistry>();
        return services;
    }
}
