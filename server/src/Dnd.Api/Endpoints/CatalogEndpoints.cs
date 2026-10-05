using Dnd.Api.Filters;
using Dnd.Application.Catalog;

namespace Dnd.Api.Endpoints;

public static class CatalogEndpoints
{
    public static IEndpointRouteBuilder MapCatalogEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/catalog")
            .WithTags("Catalog")
            .RequireAuthorization()
            .AddEndpointFilter<ValidationFilter>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapGet("/attribution", (GetAttributionHandler handler) => TypedResults.Ok(handler.Handle()))
            .WithName("GetCatalogAttribution")
            .WithSummary("Texto de atribución CC-BY 4.0 del SRD 5.1, obligatorio en la app.");

        group.MapGet("/classes", async (ListClassesHandler handler, CancellationToken ct) =>
                TypedResults.Ok(await handler.HandleAsync(ct)))
            .WithName("ListCatalogClasses")
            .WithSummary("Clases del SRD ordenadas por nombre.");

        group.MapGet("/classes/{index}", async (string index, GetClassHandler handler, CancellationToken ct) =>
                TypedResults.Ok(await handler.HandleAsync(index, ct)))
            .WithName("GetCatalogClass")
            .WithSummary("Detalle de una clase: niveles con espacios de conjuro y rasgos, y subclases con sus rasgos.")
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/races", async (ListRacesHandler handler, CancellationToken ct) =>
                TypedResults.Ok(await handler.HandleAsync(ct)))
            .WithName("ListCatalogRaces")
            .WithSummary("Razas del SRD ordenadas por nombre.");

        group.MapGet("/races/{index}", async (string index, GetRaceHandler handler, CancellationToken ct) =>
                TypedResults.Ok(await handler.HandleAsync(index, ct)))
            .WithName("GetCatalogRace")
            .WithSummary("Detalle de una raza con sus rasgos y subrazas.")
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/spells", async ([AsParameters] SearchSpellsQuery query, SearchSpellsHandler handler, CancellationToken ct) =>
                TypedResults.Ok(await handler.HandleAsync(query, ct)))
            .WithName("SearchCatalogSpells")
            .WithSummary("Conjuros paginados con búsqueda por nombre y filtros (nivel, clase, escuela, ritual, concentración).")
            .ProducesValidationProblem();

        group.MapGet("/spells/{index}", async (string index, GetSpellHandler handler, CancellationToken ct) =>
                TypedResults.Ok(await handler.HandleAsync(index, ct)))
            .WithName("GetCatalogSpell")
            .WithSummary("Detalle de un conjuro.")
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/items", async ([AsParameters] SearchItemsQuery query, SearchItemsHandler handler, CancellationToken ct) =>
                TypedResults.Ok(await handler.HandleAsync(query, ct)))
            .WithName("SearchCatalogItems")
            .WithSummary("Objetos del SRD paginados con búsqueda por nombre y filtros de categoría y rareza.")
            .ProducesValidationProblem();

        group.MapGet("/items/{id:guid}", async (Guid id, GetItemHandler handler, CancellationToken ct) =>
                TypedResults.Ok(await handler.HandleAsync(id, ct)))
            .WithName("GetCatalogItem")
            .WithSummary("Detalle de un objeto del SRD.")
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/conditions", async (ListConditionsHandler handler, CancellationToken ct) =>
                TypedResults.Ok(await handler.HandleAsync(ct)))
            .WithName("ListCatalogConditions")
            .WithSummary("Condiciones del SRD ordenadas por nombre.");

        group.MapGet("/skills", async (ListSkillsHandler handler, CancellationToken ct) =>
                TypedResults.Ok(await handler.HandleAsync(ct)))
            .WithName("ListCatalogSkills")
            .WithSummary("Habilidades del SRD ordenadas por nombre.");

        group.MapGet("/backgrounds", async (ListBackgroundsHandler handler, CancellationToken ct) =>
                TypedResults.Ok(await handler.HandleAsync(ct)))
            .WithName("ListCatalogBackgrounds")
            .WithSummary("Trasfondos del SRD ordenados por nombre.");

        group.MapGet("/features/{index}", async (string index, GetFeatureHandler handler, CancellationToken ct) =>
                TypedResults.Ok(await handler.HandleAsync(index, ct)))
            .WithName("GetCatalogFeature")
            .WithSummary("Detalle de un rasgo de clase o subclase.")
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }
}
