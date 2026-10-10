using OpenTrpg.Core.Application.Common;

namespace OpenTrpg.Core.Application.Campaigns;

/// <summary>Shared errors of campaign-scoped use cases (also used by the <c>ICampaignAccess</c> implementation).</summary>
public static class CampaignErrors
{
    /// <summary>Also used for non-members, so the existence of the campaign is not revealed.</summary>
    public static AppException CampaignNotFound() => AppException.NotFound("Campaña no encontrada.");

    public static AppException InsufficientRole() => AppException.Forbidden("No tienes permisos suficientes en esta campaña.");
}
