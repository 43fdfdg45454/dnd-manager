using System.Text.Json;

namespace OpenTrpg.Core.Application.Systems;

/// <summary>Rests a player asks the DM for: the kinds the system has, its payload and what approving it does.</summary>
public interface IRestSystem
{
    /// <summary>Kinds of rest (for example <c>short</c> and <c>long</c>).</summary>
    IReadOnlyList<string> Kinds { get; }

    /// <summary>Checks the payload of a rest request of a kind (throws a validation error when it is not valid).</summary>
    Task ValidateRequestAsync(CharacterRef character, string kind, JsonElement payload, CancellationToken cancellationToken = default);

    /// <summary>Applies an approved rest request.</summary>
    Task ApplyAsync(CharacterRef character, string kind, JsonElement payload, DateTimeOffset now, CancellationToken cancellationToken = default);
}
