using Microsoft.Extensions.DependencyInjection;
using OpenTrpg.Core.Application.Abstractions;
using OpenTrpg.Core.Application.Abstractions.Persistence;
using OpenTrpg.Core.Application.Systems.Dnd5e;
using OpenTrpg.Core.Infrastructure.Persistence.Repositories;
using OpenTrpg.Core.Application.Catalog;

namespace OpenTrpg.Core.Infrastructure.Catalog;

/// <summary>The infrastructure of the D&amp;D 5e module: catalog and character repositories, SRD seed and packs.</summary>
public static class Dnd5eInfrastructureServices
{
    public static IServiceCollection AddDnd5eInfrastructure(this IServiceCollection services)
    {
        services.AddScoped<ICatalogRepository, CatalogRepository>();
        services.AddScoped<IDnd5eCharacterRepository, Dnd5eCharacterRepository>();
        services.AddScoped<ICharacterCompanionRepository, CharacterCompanionRepository>();
        services.AddScoped<ISrdSeeder, SrdSeeder>();
        services.AddScoped<IDnd5eCatalogSystem, Dnd5eCatalogSystem>();
        services.AddSingleton<IBeastCatalog, SrdBeastCatalog>();

        return services;
    }
}
