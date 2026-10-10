using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace OpenTrpg.Core.Application.Systems;

public static class GameSystemServiceCollectionExtensions
{
    /// <summary>
    /// Registers a game system (per request scope: its parts use scoped services), the <see cref="IGameSystemRegistry"/>
    /// that lists the systems and <see cref="CampaignSystems"/>.
    /// </summary>
    public static IServiceCollection AddGameSystem<TSystem>(this IServiceCollection services)
        where TSystem : class, IGameSystem
    {
        services.TryAddScoped<TSystem>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IGameSystem, TSystem>(sp => sp.GetRequiredService<TSystem>()));
        services.TryAddScoped<IGameSystemRegistry, GameSystemRegistry>();
        services.TryAddScoped<CampaignSystems>();
        return services;
    }
}
