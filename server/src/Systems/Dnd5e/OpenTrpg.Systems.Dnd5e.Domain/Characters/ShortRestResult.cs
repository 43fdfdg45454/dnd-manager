using OpenTrpg.Core.Domain.Characters;
using OpenTrpg.Systems.Dnd5e.Domain.Characters;
namespace OpenTrpg.Systems.Dnd5e.Domain.Characters;

/// <summary>One hit die spent on a short rest: the roll and the hit points it restored (roll + Con, at least 0).</summary>
public sealed record HitDieRoll(string ClassIndex, int Die, int Roll, int Healing);

/// <summary>Outcome of <see cref="Character.ShortRest(IReadOnlyDictionary{string, int}, IReadOnlyDictionary{string, int}, int, int, IDiceRoller, DateTimeOffset)"/>.</summary>
/// <param name="Rolls">Every die spent, in the order rolled.</param>
/// <param name="HitPointsRestored">Hit points actually gained (capped by the maximum).</param>
public sealed record ShortRestResult(IReadOnlyList<HitDieRoll> Rolls, int HitPointsRestored);
