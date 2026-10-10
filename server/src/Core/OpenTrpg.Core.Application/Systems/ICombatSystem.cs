using System.Text.Json.Nodes;

namespace OpenTrpg.Core.Application.Systems;

/// <summary>Combat tracking of a character: the combat view, damage, healing and conditions.</summary>
public interface ICombatSystem
{
    /// <summary>The precalculated data of the combat view.</summary>
    Task<JsonObject> BuildSummaryAsync(CharacterRef character, SystemSheet sheet, CancellationToken cancellationToken = default);

    /// <summary>Applies damage; returns what happened (in the system's terms, for example a death save failed).</summary>
    Task<JsonObject> ApplyDamageAsync(CharacterRef character, int amount, DateTimeOffset now, CancellationToken cancellationToken = default);

    /// <summary>Heals the character (capped by the system).</summary>
    Task HealAsync(CharacterRef character, int amount, DateTimeOffset now, CancellationToken cancellationToken = default);

    /// <summary>Adds and removes conditions by index.</summary>
    Task SetConditionsAsync(
        CharacterRef character,
        IReadOnlyList<string> add,
        IReadOnlyList<string> remove,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);
}
