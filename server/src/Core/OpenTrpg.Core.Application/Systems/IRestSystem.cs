using System.Text.Json;
using System.Text.Json.Nodes;

namespace OpenTrpg.Core.Application.Systems;

/// <summary>Rests a player asks the DM for: the kinds the system has, its payload and what approving it does.</summary>
public interface IRestSystem
{
    /// <summary>Kinds of rest, as stored and shown (for example <c>Short</c> and <c>Long</c>; requests match them in any case).</summary>
    IReadOnlyList<string> Kinds { get; }

    /// <summary>
    /// Checks the payload of a rest request of a kind (throws a validation error when it is not valid) and returns it
    /// normalized, as the core stores it.
    /// </summary>
    Task<JsonObject> ValidateRequestAsync(CharacterRef character, string kind, JsonElement payload, CancellationToken cancellationToken = default);

    /// <summary>Applies an approved rest request (the core saves afterwards).</summary>
    Task ApplyAsync(CharacterRef character, string kind, JsonElement payload, DateTimeOffset now, CancellationToken cancellationToken = default);
}
