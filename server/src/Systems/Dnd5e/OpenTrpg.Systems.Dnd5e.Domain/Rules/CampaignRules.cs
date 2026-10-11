namespace OpenTrpg.Systems.Dnd5e.Domain.Rules;

/// <summary>
/// Optional rules of a D&amp;D 5e campaign that the Owner or a DM switches on next to its content packs. One row per
/// campaign at most; a campaign without a row uses the defaults (everything off).
/// </summary>
public sealed class Dnd5eCampaignRules
{
    private Dnd5eCampaignRules()
    {
    }

    public Guid CampaignId { get; private set; }

    /// <summary>
    /// Starter feats: at level 1 a character may take one feat marked <c>starter</c> of the campaign's packs, on top of
    /// everything else (the optional "Dote inicial" choice).
    /// </summary>
    public bool StarterFeatsEnabled { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static Dnd5eCampaignRules Create(Guid campaignId, DateTimeOffset now) => new()
    {
        CampaignId = campaignId,
        UpdatedAt = now,
    };

    /// <summary>True when the value changed.</summary>
    public bool SetStarterFeats(bool enabled, DateTimeOffset now)
    {
        if (StarterFeatsEnabled == enabled)
        {
            return false;
        }

        StarterFeatsEnabled = enabled;
        UpdatedAt = now;
        return true;
    }
}
