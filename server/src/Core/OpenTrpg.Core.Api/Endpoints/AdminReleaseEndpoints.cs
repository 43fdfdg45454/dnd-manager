using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using OpenTrpg.Core.Api.Auth;
using OpenTrpg.Core.Api.Filters;
using OpenTrpg.Core.Application.Abstractions;
using OpenTrpg.Core.Application.Common;
using OpenTrpg.Core.Application.Releases;
using FluentValidation;

namespace OpenTrpg.Core.Api.Endpoints;

/// <summary>
/// Body of <c>POST /api/v1/admin/releases</c> (documentation only; the endpoint reads the request itself).
/// Multipart carries <c>file</c>; JSON (<c>application/json</c>) has no file and uses <c>fileId</c> instead.
/// </summary>
/// <param name="File">The APK (multipart only).</param>
/// <param name="Version">Semantic version <c>major.minor.patch</c>.</param>
/// <param name="BuildNumber">Positive integer, unique.</param>
/// <param name="Notes">Release notes (optional).</param>
/// <param name="IsMandatory">true to block the app until it is updated (default false).</param>
/// <param name="FileId">APK already uploaded with kind AppRelease, instead of <c>file</c>.</param>
public sealed record PublishReleaseForm(IFormFile? File, string Version, int BuildNumber, string? Notes, bool? IsMandatory, Guid? FileId);

/// <summary>Administration of the APK releases and the instance statistics.</summary>
public static class AdminReleaseEndpoints
{
    public static IEndpointRouteBuilder MapAdminReleaseEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/admin")
            .WithTags("Admin")
            .RequireAuthorization(AuthPolicies.Admin)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapGet("/releases", async (ListReleasesHandler handler, CancellationToken ct) =>
                TypedResults.Ok(await handler.HandleAsync(ct)))
            .WithName("ListReleases")
            .WithSummary("Releases del APK, de la más reciente a la más antigua.");

        group.MapPost("/releases", PublishAsync)
            .DisableAntiforgery()
            .Accepts<PublishReleaseForm>("multipart/form-data", "application/json")
            .Produces<ReleaseDto>(StatusCodes.Status201Created)
            .WithName("PublishRelease")
            .WithSummary("Publica una release. Multipart: file (APK), version, buildNumber, notes?, isMandatory?; o JSON con fileId de un APK ya subido con kind AppRelease. 409 si la versión o el número de compilación ya existen.")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status413PayloadTooLarge);

        group.MapDelete("/releases/{id:guid}", async (Guid id, DeleteReleaseHandler handler, CancellationToken ct) =>
            {
                await handler.HandleAsync(id, ct);
                return TypedResults.NoContent();
            })
            .WithName("DeleteRelease")
            .WithSummary("Borra la release y su APK.")
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/stats", async (GetAdminStatsHandler handler, CancellationToken ct) =>
                TypedResults.Ok(await handler.HandleAsync(AppEndpoints.ApiVersion, ct)))
            .WithName("GetAdminStats")
            .WithSummary("Versión de la API y conteos de usuarios, campañas, personajes, sesiones programadas y ficheros, más la última release.");

        return app;
    }

    private static async Task<IResult> PublishAsync(
        HttpRequest request,
        ClaimsPrincipal user,
        IFileStorage storage,
        IValidator<PublishReleaseRequest> validator,
        PublishReleaseHandler handler,
        CancellationToken cancellationToken)
    {
        PublishReleaseRequest metadata;
        IFormFile? file = null;
        if (request.HasJsonContentType())
        {
            try
            {
                metadata = await request.ReadFromJsonAsync<PublishReleaseRequest>(cancellationToken)
                    ?? throw AppException.Validation("body", "Falta el cuerpo de la petición.");
            }
            catch (JsonException)
            {
                throw AppException.Validation("body", "El cuerpo de la petición no es un JSON válido.");
            }
        }
        else
        {
            var form = await FileEndpoints.ReadUploadFormAsync(request, storage, cancellationToken);
            file = form.Files.GetFile("file");
            metadata = new PublishReleaseRequest(
                NullIfEmpty(form["version"].ToString()),
                ParseInt(form, "buildNumber"),
                NullIfEmpty(form["notes"].ToString()),
                ParseBool(form, "isMandatory"),
                ParseGuid(form, "fileId"));
        }

        var validation = await validator.ValidateAsync(metadata, cancellationToken);
        if (!validation.IsValid)
        {
            return ValidationProblems.From(validation);
        }

        await using var content = file?.OpenReadStream();
        var release = await handler.HandleAsync(user.GetUserId(), metadata, content, file?.FileName, cancellationToken);
        return TypedResults.Created("/api/v1/admin/releases", release);
    }

    private static string? NullIfEmpty(string value) => value.Length == 0 ? null : value;

    private static int? ParseInt(IFormCollection form, string field)
    {
        var value = form[field].ToString().Trim();
        if (value.Length == 0)
        {
            return null;
        }

        return int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var number)
            ? number
            : throw AppException.Validation(field, "El número de compilación debe ser un entero positivo.");
    }

    private static bool? ParseBool(IFormCollection form, string field)
    {
        var value = form[field].ToString().Trim();
        return value.ToLowerInvariant() switch
        {
            "" => null,
            "true" or "1" or "on" => true,
            "false" or "0" or "off" => false,
            _ => throw AppException.Validation(field, "El valor debe ser true o false."),
        };
    }

    private static Guid? ParseGuid(IFormCollection form, string field)
    {
        var value = form[field].ToString().Trim();
        if (value.Length == 0)
        {
            return null;
        }

        return Guid.TryParse(value, out var id) ? id : throw AppException.Validation(field, "El identificador no es válido.");
    }
}
