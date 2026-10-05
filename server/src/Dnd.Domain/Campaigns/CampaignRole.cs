namespace Dnd.Domain.Campaigns;

/// <summary>
/// Role of a user inside a campaign. Values are ordered by rank (Owner &gt; DM &gt; Player), so
/// "at least DM" is <c>role &gt;= CampaignRole.DM</c>. Persisted as its name.
/// </summary>
public enum CampaignRole
{
    Player = 0,
    DM = 1,
    Owner = 2,
}

public static class CampaignRoleExtensions
{
    /// <summary>True when <paramref name="role"/> ranks the same as or above <paramref name="minimum"/>.</summary>
    public static bool IsAtLeast(this CampaignRole role, CampaignRole minimum) => role >= minimum;
}
