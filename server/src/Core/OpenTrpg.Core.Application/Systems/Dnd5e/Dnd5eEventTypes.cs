using OpenTrpg.Core.Application.Abstractions;

namespace OpenTrpg.Core.Application.Systems.Dnd5e;

/// <summary>Realtime event kinds of the D&amp;D 5e system (in addition to <see cref="CampaignEventTypes"/>).</summary>
public static class Dnd5eEventTypes
{
    /// <summary>The DM forced a rest on the party (<see cref="CampaignEvent.EntityId"/> is null).</summary>
    public const string PartyRest = "party.rest";

    /// <summary>A DM granted a level-up to the character (sent to the campaign and to the character's owner).</summary>
    public const string LevelUpGranted = "levelUp.granted";

    /// <summary>The character completed a level-up (sent to the campaign, together with <see cref="CampaignEventTypes.CharacterUpdated"/>).</summary>
    public const string LevelUpCompleted = "levelUp.completed";

    public static IReadOnlyList<string> All { get; } = [PartyRest, LevelUpGranted, LevelUpCompleted];
}
