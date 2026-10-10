using OpenTrpg.Core.Application.Releases;
using Microsoft.Net.Http.Headers;

namespace OpenTrpg.Core.Api.Endpoints;

public sealed record AppInfoResponse(string Name, string Version);

/// <summary>Public endpoints of the app: API info, latest APK release and its anonymous download.</summary>
public static class AppEndpoints
{
    /// <summary>Semantic version of the API assembly (<c>major.minor.patch</c>).</summary>
    public static string ApiVersion { get; } = typeof(AppEndpoints).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";

    public static IEndpointRouteBuilder MapAppEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/app").WithTags("App").AllowAnonymous();

        group.MapGet("/info", () => TypedResults.Ok(new AppInfoResponse("dnd-companion-api", ApiVersion)))
            .WithName("GetAppInfo")
            .WithSummary("Nombre y versión de la API. Lo usa el cliente para comprobar conectividad.");

        group.MapGet("/latest", async (HttpContext http, GetLatestReleaseHandler handler, CancellationToken ct) =>
            {
                // The app polls this to learn about updates: never serve a stale answer.
                http.Response.Headers.CacheControl = "no-cache";
                var latest = await handler.HandleAsync(ct);
                return latest is null ? Results.NoContent() : Results.Ok(latest);
            })
            .Produces<LatestReleaseDto>()
            .Produces(StatusCodes.Status204NoContent)
            .WithName("GetLatestRelease")
            .WithSummary("Última versión del APK por número de compilación (200), o 204 si no se ha publicado ninguna. Anónimo.");

        group.MapGet("/download/{buildNumber:int}", async (int buildNumber, HttpContext http, DownloadReleaseHandler handler, CancellationToken ct) =>
            {
                var file = await handler.HandleAsync(buildNumber, ct);

                // A build never changes once published. Anonymous so the APK can be installed from a browser.
                http.Response.Headers.CacheControl = "public, max-age=86400";
                http.Response.Headers.XContentTypeOptions = "nosniff";
                return Results.File(
                    file.Content,
                    file.ContentType,
                    fileDownloadName: file.FileName,
                    lastModified: file.LastModified,
                    entityTag: new EntityTagHeaderValue(file.ETag),
                    enableRangeProcessing: true);
            })
            .Produces(StatusCodes.Status200OK, contentType: "application/vnd.android.package-archive")
            .Produces(StatusCodes.Status206PartialContent, contentType: "application/vnd.android.package-archive")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithName("DownloadRelease")
            .WithSummary("Descarga anónima del APK de un número de compilación (Content-Disposition dnd-companion-{versión}.apk, soporta Range).");

        return app;
    }
}
