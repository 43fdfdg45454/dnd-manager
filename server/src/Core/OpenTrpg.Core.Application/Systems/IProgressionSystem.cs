using System.Text.Json;
using System.Text.Json.Nodes;

namespace OpenTrpg.Core.Application.Systems;

/// <summary>Advancement of a character (in D&amp;D 5e, levels granted by the DM and completed by the player).</summary>
public interface IProgressionSystem
{
    /// <summary>What the next advancement offers and asks for (<paramref name="query"/>: the system's options, if any).</summary>
    Task<JsonObject> PlanAsync(CharacterRef character, JsonElement? query, CancellationToken cancellationToken = default);

    /// <summary>Applies an advancement the player (or a DM, <paramref name="actorIsDm"/>) completed.</summary>
    Task ApplyAsync(CharacterRef character, JsonElement request, bool actorIsDm, CancellationToken cancellationToken = default);

    /// <summary>Grants the next advancement to the characters.</summary>
    Task GrantAsync(IReadOnlyList<CharacterRef> characters, Guid grantedBy, DateTimeOffset now, CancellationToken cancellationToken = default);

    /// <summary>Withdraws an advancement granted and not completed yet.</summary>
    Task RevokeAsync(IReadOnlyList<CharacterRef> characters, DateTimeOffset now, CancellationToken cancellationToken = default);
}
