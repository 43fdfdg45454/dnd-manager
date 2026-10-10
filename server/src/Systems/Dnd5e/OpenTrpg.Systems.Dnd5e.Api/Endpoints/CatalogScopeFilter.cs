using System.Security.Claims;
using OpenTrpg.Core.Api.Auth;
using OpenTrpg.Core.Application.Abstractions;
using OpenTrpg.Core.Application.Common;
using OpenTrpg.Core.Application.ContentPacks;
using OpenTrpg.Core.Domain.Campaigns;

namespace OpenTrpg.Systems.Dnd5e.Api.Endpoints;

/// <summary>
/// <c>?campaignId=</c> on the catalog routes: the request sees the catalog of that campaign (the SRD, the packs it enabled
/// and its homebrew); the caller must be a member (404 otherwise). Without it, the global catalog (the SRD and every
/// imported pack).
/// </summary>
internal sealed class CatalogScopeFilter : IEndpointFilter
{
    public const string QueryName = "campaignId";

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var http = context.HttpContext;
        if (http.Request.Query.TryGetValue(QueryName, out var values) && values.Count > 0 && !string.IsNullOrWhiteSpace(values[0]))
        {
            if (!Guid.TryParse(values[0], out var campaignId) || campaignId == Guid.Empty)
            {
                throw AppException.Validation(QueryName, "La campaña no es válida.");
            }

            var services = http.RequestServices;
            await services.GetRequiredService<ICampaignAccess>()
                .RequireAsync(campaignId, http.User.GetUserId(), CampaignRole.Player, http.RequestAborted);
            await services.GetRequiredService<CatalogScopeContext>().UseCampaignAsync(campaignId, http.RequestAborted);
        }

        return await next(context);
    }
}
