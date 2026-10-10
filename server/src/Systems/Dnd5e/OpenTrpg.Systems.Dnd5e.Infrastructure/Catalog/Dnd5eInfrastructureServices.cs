using OpenTrpg.Core.Infrastructure.Persistence;
using OpenTrpg.Systems.Dnd5e.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using OpenTrpg.Core.Application.Abstractions;
using OpenTrpg.Core.Application.Abstractions.Persistence;
using OpenTrpg.Core.Infrastructure.Persistence.Repositories;
using OpenTrpg.Core.Infrastructure;
using OpenTrpg.Core.Infrastructure.Catalog;
using OpenTrpg.Systems.Dnd5e.Application;
using OpenTrpg.Systems.Dnd5e.Application.Abstractions;
using OpenTrpg.Systems.Dnd5e.Application.Abstractions.Persistence;
using OpenTrpg.Systems.Dnd5e.Application.Catalog;
using OpenTrpg.Systems.Dnd5e.Infrastructure.Catalog;
using OpenTrpg.Systems.Dnd5e.Infrastructure.Persistence.Repositories;

namespace OpenTrpg.Systems.Dnd5e.Infrastructure.Catalog;

/// <summary>The infrastructure of the D&amp;D 5e module: catalog and character repositories, SRD seed and packs.</summary>
public static class Dnd5eInfrastructureServices
{
    public static IServiceCollection AddDnd5eInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IModelConfigurator, Dnd5eModelConfigurator>();
        services.AddScoped<ICatalogRepository, CatalogRepository>();
        services.AddScoped<IDnd5eCharacterRepository, Dnd5eCharacterRepository>();
        services.AddScoped<ICharacterCompanionRepository, CharacterCompanionRepository>();
        services.AddScoped<ISrdSeeder, SrdSeeder>();
        services.AddScoped<IDnd5eCatalogSystem, Dnd5eCatalogSystem>();
        services.AddSingleton<IBeastCatalog, SrdBeastCatalog>();

        return services;
    }
}
