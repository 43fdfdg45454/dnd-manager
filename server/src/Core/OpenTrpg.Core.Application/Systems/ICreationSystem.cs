using System.Text.Json;
using System.Text.Json.Nodes;

namespace OpenTrpg.Core.Application.Systems;

/// <summary>Creation of characters: the part of the system of a new character, origins and activation.</summary>
public interface ICreationSystem
{
    /// <summary>
    /// Adds the system's part of a new core character (to the same unit of work) with its initial values;
    /// <paramref name="creation"/> carries the system's creation data, if any.
    /// </summary>
    Task InitializeAsync(CharacterRef character, JsonElement? creation, CancellationToken cancellationToken = default);

    /// <summary>The origin choices of the character (what is chosen and what can be chosen).</summary>
    Task<JsonObject> GetOriginChoicesAsync(CharacterRef character, CancellationToken cancellationToken = default);

    /// <summary>Saves the answers to the origin choices.</summary>
    Task SaveOriginChoicesAsync(CharacterRef character, JsonElement answers, CancellationToken cancellationToken = default);

    /// <summary>
    /// Throws when the draft cannot be activated yet (for example, origin choices left). The core calls it before
    /// submitting a draft for activation.
    /// </summary>
    Task EnsureReadyForActivationAsync(CharacterRef character, CancellationToken cancellationToken = default);

    /// <summary>
    /// Prepares a draft that is being activated (complete origins, full hit points, initial spell preparation…). The
    /// core calls it right after changing the character's status to active, in the same unit of work.
    /// </summary>
    Task PrepareActivationAsync(CharacterRef character, DateTimeOffset now, CancellationToken cancellationToken = default);
}
