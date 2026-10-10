using OpenTrpg.Core.Domain.Characters;
using OpenTrpg.Core.Application;
using OpenTrpg.Core.Application.Characters;
using OpenTrpg.Systems.Dnd5e.Application;
using OpenTrpg.Systems.Dnd5e.Application.Characters;
using OpenTrpg.Systems.Dnd5e.Domain.Characters;

namespace OpenTrpg.Systems.Dnd5e.Application.Characters;

/// <summary>The pending rest of a character, as shown on the sheet and at the DM's table.</summary>
/// <param name="Kind">"Short" or "Long".</param>
/// <param name="HitDice">Hit dice the player wants to spend per class index (short rest only).</param>
public sealed record PendingRestDto(Guid Id, string Kind, IReadOnlyDictionary<string, int> HitDice, DateTimeOffset RequestedAt)
{
    public static PendingRestDto From(RestRequest request) =>
        new(request.Id, request.Kind, Dnd5eRestPayload.HitDice(request), request.RequestedAt);
}
