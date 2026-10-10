using System.Security.Claims;
using OpenTrpg.Core.Api.Auth;
using OpenTrpg.Core.Api.Filters;
using OpenTrpg.Core.Api.Hosting;
using OpenTrpg.Core.Application.Auth;

namespace OpenTrpg.Core.Api.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/auth")
            .WithTags("Auth")
            .AddEndpointFilter<ValidationFilter>();

        group.MapPost("/login", async (LoginRequest request, LoginHandler handler, CancellationToken ct) =>
                TypedResults.Ok(await handler.HandleAsync(request, ct)))
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitingSetup.LoginPolicy)
            .AddEndpointFilter<LoginAccountRateLimitFilter>()
            .WithName("Login")
            .WithSummary("Inicia sesión con email y contraseña. 429 tras demasiados intentos por IP o por cuenta.")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        group.MapPost("/refresh", async (RefreshRequest request, RefreshHandler handler, CancellationToken ct) =>
                TypedResults.Ok(await handler.HandleAsync(request, ct)))
            .AllowAnonymous()
            .WithName("RefreshToken")
            .WithSummary("Rota el refresh token y emite un nuevo access token.")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapPost("/logout", async (LogoutRequest request, ClaimsPrincipal user, LogoutHandler handler, CancellationToken ct) =>
            {
                await handler.HandleAsync(user.GetUserId(), request, ct);
                return TypedResults.NoContent();
            })
            .RequireAuthorization()
            .WithName("Logout")
            .WithSummary("Revoca el refresh token indicado.")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapPost("/password/forgot", async (ForgotPasswordRequest request, ForgotPasswordHandler handler, CancellationToken ct) =>
            {
                await handler.HandleAsync(request, ct);
                return TypedResults.Accepted((string?)null);
            })
            .AllowAnonymous()
            .WithName("ForgotPassword")
            .WithSummary("Envía un enlace para restablecer la contraseña. Responde 202 siempre.")
            .ProducesValidationProblem();

        group.MapPost("/password/set", async (SetPasswordRequest request, SetPasswordHandler handler, CancellationToken ct) =>
            {
                await handler.HandleAsync(request, ct);
                return TypedResults.NoContent();
            })
            .AllowAnonymous()
            .WithName("SetPassword")
            .WithSummary("Establece la contraseña con un token de alta o de restablecimiento.")
            .ProducesValidationProblem();

        group.MapGet("/me", async (ClaimsPrincipal user, GetMeHandler handler, CancellationToken ct) =>
                TypedResults.Ok(await handler.HandleAsync(user.GetUserId(), ct)))
            .RequireAuthorization()
            .WithName("GetMe")
            .WithSummary("Usuario autenticado.")
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapPatch("/me", async (UpdateProfileRequest request, ClaimsPrincipal user, UpdateProfileHandler handler, CancellationToken ct) =>
                TypedResults.Ok(await handler.HandleAsync(user.GetUserId(), request, ct)))
            .RequireAuthorization()
            .WithName("UpdateMe")
            .WithSummary("Edita el nombre visible y si el usuario recibe correos (notificationsEnabled).")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        return app;
    }
}
