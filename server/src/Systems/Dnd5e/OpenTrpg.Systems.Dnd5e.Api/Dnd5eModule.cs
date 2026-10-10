using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using OpenTrpg.Core.Application.Systems;
using OpenTrpg.Systems.Dnd5e.Api.Endpoints;
using OpenTrpg.Systems.Dnd5e.Application;
using OpenTrpg.Systems.Dnd5e.Infrastructure.Catalog;

namespace OpenTrpg.Systems.Dnd5e.Api;

/// <summary>Registration of the D&amp;D 5e module in the API host.</summary>
public static class Dnd5eModule
{
    /// <summary>The game system, its services and its part of the database model.</summary>
    public static IServiceCollection AddDnd5eSystem(this IServiceCollection services)
    {
        services.AddGameSystem<Dnd5eSystem>();
        services.AddDnd5eApplication();
        services.AddDnd5eInfrastructure();
        return services;
    }

    /// <summary>The routes of the module (catalog, 5e character routes, party and level-up), at their current paths.</summary>
    public static IEndpointRouteBuilder MapDnd5eEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapCatalogEndpoints();
        app.MapDnd5eCharacterEndpoints();
        app.MapPartyEndpoints();
        app.MapLevelUpEndpoints();
        return app;
    }
}
