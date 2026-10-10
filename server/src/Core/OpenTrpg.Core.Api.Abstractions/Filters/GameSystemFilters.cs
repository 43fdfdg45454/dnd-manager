using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using OpenTrpg.Core.Application.Abstractions.Persistence;
using OpenTrpg.Core.Application.Common;

namespace OpenTrpg.Core.Api.Filters;

/// <summary>
/// Endpoint filters that scope the routes of a game system (<c>/api/v1/systems/{systemId}/...</c>) to the
/// campaigns of that system: a character or campaign of another system answers <c>404</c>, as if it did not
/// exist. A missing character or campaign is left to the handler, which answers its own 404.
/// </summary>
public static class GameSystemFilters
{
    /// <summary>The route belongs to a character (route value <paramref name="routeParameter"/>) of a campaign of <paramref name="systemId"/>.</summary>
    public static TBuilder RequireCharacterSystem<TBuilder>(this TBuilder builder, string systemId, string routeParameter = "id")
        where TBuilder : IEndpointConventionBuilder =>
        builder.AddEndpointFilter(async (context, next) =>
        {
            var http = context.HttpContext;
            if (TryGetGuid(http, routeParameter, out var characterId))
            {
                var ownership = await http.RequestServices.GetRequiredService<ICharacterRepository>()
                    .GetOwnershipAsync(characterId, http.RequestAborted);
                if (ownership is not null)
                {
                    await EnsureSystemAsync(http, ownership.CampaignId, systemId, "El personaje no existe.");
                }
            }

            return await next(context);
        });

    /// <summary>The route belongs to a campaign (route value <paramref name="routeParameter"/>) of <paramref name="systemId"/>.</summary>
    public static TBuilder RequireCampaignSystem<TBuilder>(this TBuilder builder, string systemId, string routeParameter = "campaignId")
        where TBuilder : IEndpointConventionBuilder =>
        builder.AddEndpointFilter(async (context, next) =>
        {
            var http = context.HttpContext;
            if (TryGetGuid(http, routeParameter, out var campaignId))
            {
                await EnsureSystemAsync(http, campaignId, systemId, "La campaña no existe.");
            }

            return await next(context);
        });

    private static bool TryGetGuid(HttpContext http, string routeParameter, out Guid value)
    {
        value = Guid.Empty;
        return http.Request.RouteValues.TryGetValue(routeParameter, out var raw)
            && Guid.TryParse(raw?.ToString(), out value);
    }

    private static async Task EnsureSystemAsync(HttpContext http, Guid campaignId, string systemId, string notFoundMessage)
    {
        var campaignSystem = await http.RequestServices.GetRequiredService<ICampaignRepository>()
            .GetSystemIdAsync(campaignId, http.RequestAborted);
        if (campaignSystem is not null && !string.Equals(campaignSystem, systemId, StringComparison.Ordinal))
        {
            throw AppException.NotFound(notFoundMessage);
        }
    }
}
