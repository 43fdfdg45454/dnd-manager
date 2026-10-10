using System.Text.Json;
using System.Text.Json.Nodes;

namespace OpenTrpg.Core.Application.Systems;

/// <summary>Choices of a character whose prerequisites no longer hold (they must be replaced).</summary>
public interface IChoiceSystem
{
    /// <summary>The invalid choices of the character, each one as the system describes it.</summary>
    Task<IReadOnlyList<JsonObject>> FindInvalidAsync(CharacterRef character, CancellationToken cancellationToken = default);

    /// <summary>Replaces invalid choices (<paramref name="replacements"/>: the system's request body).</summary>
    Task ReplaceInvalidAsync(CharacterRef character, JsonElement replacements, DateTimeOffset now, CancellationToken cancellationToken = default);
}
