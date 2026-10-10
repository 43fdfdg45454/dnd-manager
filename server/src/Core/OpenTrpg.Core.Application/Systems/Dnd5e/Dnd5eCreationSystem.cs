using System.Text.Json;
using System.Text.Json.Nodes;
using OpenTrpg.Core.Application.Abstractions;
using OpenTrpg.Core.Application.Abstractions.Persistence;
using OpenTrpg.Core.Application.Characters;
using OpenTrpg.Core.Application.Common;
using OpenTrpg.Core.Domain.Characters;

namespace OpenTrpg.Core.Application.Systems.Dnd5e;

/// <summary>Creation and activation of D&amp;D 5e characters (<see cref="ICreationSystem"/>).</summary>
public sealed class Dnd5eCreationSystem(
    Dnd5eCharacterParts parts,
    IDnd5eCharacterRepository characters,
    ICharacterSheetService sheets,
    OriginChoicesPlanner originChoices,
    OriginChoicesHandler originChoicesHandler,
    SpellPreparationPlanner preparation,
    IDateTimeProvider clock) : ICreationSystem
{
    /// <summary>A new draft: every score at 10, average hit points, full hit points.</summary>
    public async Task InitializeAsync(CharacterRef character, JsonElement? creation, CancellationToken cancellationToken = default)
    {
        var created = Dnd5eCharacter.Create(character.Character);
        await sheets.RecalculateAsync(created, cancellationToken);
        characters.Add(created);
        character.System = created;
    }

    public async Task<JsonObject> GetOriginChoicesAsync(CharacterRef character, CancellationToken cancellationToken = default)
    {
        var loaded = await parts.LoadAsync(character, cancellationToken);
        return Dnd5eCharacterParts.ToJsonObject(OriginChoicesPlanner.ToDto(loaded, await originChoices.PlanAsync(loaded, cancellationToken)));
    }

    public async Task SaveOriginChoicesAsync(CharacterRef character, JsonElement answers, CancellationToken cancellationToken = default)
    {
        var loaded = await parts.LoadAsync(character, cancellationToken);
        var request = Dnd5eCharacterParts.FromJson<SaveOriginChoicesRequest>(answers)
            ?? throw AppException.Validation("choices", "Las respuestas no son válidas.");
        await originChoicesHandler.ApplyAsync(loaded, request, clock.UtcNow, cancellationToken);
    }

    /// <summary>Every origin choice of the race and the background must be answered.</summary>
    public async Task EnsureReadyForActivationAsync(CharacterRef character, CancellationToken cancellationToken = default) =>
        await originChoices.EnsureCompleteAsync(await parts.LoadAsync(character, cancellationToken), cancellationToken);

    /// <summary>Full hit points and, for a character that prepares spells and has none prepared, the first preparation.</summary>
    public async Task PrepareActivationAsync(CharacterRef character, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        var loaded = await parts.LoadAsync(character, cancellationToken);
        var sheet = await sheets.CalculateAsync(loaded, cancellationToken);
        loaded.EnterPlay(sheet.HitPointsMax);
        await preparation.RequireInitialPreparationAsync(loaded, now, cancellationToken);
    }
}
