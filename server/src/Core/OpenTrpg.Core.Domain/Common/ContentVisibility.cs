namespace OpenTrpg.Core.Domain.Common;

/// <summary>Who can see a lore entry, map or map pin of a campaign. Persisted as its name.</summary>
public enum ContentVisibility
{
    /// <summary>Every member of the campaign.</summary>
    Players,

    /// <summary>Only DMs (the Owner included); players get 404 / do not receive it.</summary>
    DmOnly,
}
