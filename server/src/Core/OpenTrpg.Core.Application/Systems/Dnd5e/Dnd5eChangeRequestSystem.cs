using System.Text.Json;
using System.Text.Json.Nodes;
using OpenTrpg.Core.Application.Characters;
using OpenTrpg.Core.Application.Common;
using OpenTrpg.Core.Domain.Characters;

namespace OpenTrpg.Core.Application.Systems.Dnd5e;

/// <summary>The change requests of D&amp;D 5e: sheet edits and the animal companion (<see cref="IChangeRequestSystem"/>).</summary>
public sealed class Dnd5eChangeRequestSystem(
    Dnd5eCharacterParts parts,
    Dnd5eSheetSystem sheet,
    CompanionPlanner companions) : IChangeRequestSystem
{
    public IReadOnlyList<string> Types => Dnd5eChangeRequestTypes.All;

    public async Task<JsonObject?> SnapshotAsync(CharacterRef character, string type, JsonElement payload, CancellationToken cancellationToken = default) =>
        type == Dnd5eChangeRequestTypes.EditSheet ? await sheet.SnapshotAsync(character, payload, cancellationToken) : null;

    public async Task ApplyAsync(CharacterRef character, string type, JsonElement payload, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        switch (type)
        {
            case Dnd5eChangeRequestTypes.EditSheet:
                // Sheet edit requests come from the owner of an active character, who cannot prepare spells this way.
                var patch = await sheet.ParseAsync(payload, cancellationToken, errorField: "payload");
                await sheet.ApplyAsync(character, patch, keepSpellPreparation: true, now, cancellationToken);
                break;
            case Dnd5eChangeRequestTypes.Companion:
                var loaded = await parts.LoadAsync(character, cancellationToken);
                await companions.ApplyApprovedAsync(loaded, payload.GetRawText(), now, cancellationToken);
                break;
            default:
                throw AppException.Conflict("Este tipo de solicitud todavía no está soportado.");
        }
    }
}
