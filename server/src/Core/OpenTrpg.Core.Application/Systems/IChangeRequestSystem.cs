using System.Text.Json;
using System.Text.Json.Nodes;

namespace OpenTrpg.Core.Application.Systems;

/// <summary>Change request types registered by a game system (the core applies its own types).</summary>
public interface IChangeRequestSystem
{
    /// <summary>The types the system applies (for example a sheet edit).</summary>
    IReadOnlyList<string> Types { get; }

    /// <summary>The "before" of a request of one of <see cref="Types"/>, or null when there is nothing to compare.</summary>
    Task<JsonObject?> SnapshotAsync(CharacterRef character, string type, JsonElement payload, CancellationToken cancellationToken = default);

    /// <summary>Applies an approved request of one of <see cref="Types"/> (the core recalculates the sheet and saves afterwards).</summary>
    Task ApplyAsync(CharacterRef character, string type, JsonElement payload, DateTimeOffset now, CancellationToken cancellationToken = default);
}
