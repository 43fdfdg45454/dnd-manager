using Dnd.Domain.Campaigns;

namespace Dnd.Application.Campaigns;

/// <summary>Strict parsing of the campaign role names accepted by the API ("DM" | "Player").</summary>
public static class CampaignRoles
{
    public const string AssignableMessage = "El rol debe ser \"DM\" o \"Player\".";

    /// <summary>Parses a role that can be assigned to a member. "Owner" is rejected: it only changes by transfer.</summary>
    public static bool TryParseAssignable(string? value, out CampaignRole role)
    {
        switch (value)
        {
            case nameof(CampaignRole.DM):
                role = CampaignRole.DM;
                return true;
            case nameof(CampaignRole.Player):
                role = CampaignRole.Player;
                return true;
            default:
                role = default;
                return false;
        }
    }

    public static bool IsAssignable(string? value) => TryParseAssignable(value, out _);

    /// <summary>Parses a value already accepted by a validator.</summary>
    public static CampaignRole ParseAssignable(string value) =>
        TryParseAssignable(value, out var role) ? role : throw new ArgumentOutOfRangeException(nameof(value), value, null);
}
