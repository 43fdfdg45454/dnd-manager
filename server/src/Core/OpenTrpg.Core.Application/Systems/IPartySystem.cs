using System.Text.Json;
using System.Text.Json.Nodes;

namespace OpenTrpg.Core.Application.Systems;

/// <summary>Group actions of the DM on the active characters of a campaign (loaded with <c>PartyLoader</c>).</summary>
public interface IPartySystem
{
    /// <summary>The DM's view of the characters.</summary>
    Task<JsonObject> BuildPartyAsync(IReadOnlyList<CharacterRef> characters, CancellationToken cancellationToken = default);

    /// <summary>A rest of a kind of <see cref="IRestSystem.Kinds"/> for all the characters at once.</summary>
    Task<JsonObject> RestAsync(IReadOnlyList<CharacterRef> characters, string kind, DateTimeOffset now, CancellationToken cancellationToken = default);

    /// <summary>Quick adjustments (the system's request body), for example hit points and conditions.</summary>
    Task<JsonObject> AdjustAsync(IReadOnlyList<CharacterRef> characters, JsonElement adjustments, DateTimeOffset now, CancellationToken cancellationToken = default);
}
