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

    /// <summary>Prefix of every route of the module.</summary>
    public const string RoutePrefix = "/api/v1/systems/" + Dnd5eSystem.SystemId;

    /// <summary>Swagger tag of a group of module routes, so the document groups the routes by game system.</summary>
    public static string Tag(string area) => $"{Dnd5eSystem.SystemId} · {area}";

    /// <summary>
    /// The routes of the module (catalog, 5e character routes, party and level-up) under <see cref="RoutePrefix"/>.
    /// Character and campaign routes answer 404 when the campaign belongs to another game system.
    /// </summary>
    public static IEndpointRouteBuilder MapDnd5eEndpoints(this IEndpointRouteBuilder app)
    {
        var system = app.MapGroup(RoutePrefix);
        system.MapCatalogEndpoints();
        system.MapDnd5eCharacterEndpoints();
        system.MapPartyEndpoints();
        system.MapLevelUpEndpoints();
        return app;
    }
}
