using System.Text.Json;
using System.Text.Json.Nodes;
using OpenTrpg.Core.Application.Characters;
using OpenTrpg.Core.Application.Common;
using OpenTrpg.Core.Application.Party;
using OpenTrpg.Core.Domain.Characters;
using OpenTrpg.Core.Application.Abstractions;
using OpenTrpg.Core.Application;
using OpenTrpg.Core.Application.Systems;
using OpenTrpg.Systems.Dnd5e.Application;
using OpenTrpg.Systems.Dnd5e.Application.Abstractions;
using OpenTrpg.Systems.Dnd5e.Application.Characters;
using OpenTrpg.Systems.Dnd5e.Application.Party;
using OpenTrpg.Systems.Dnd5e.Domain.Characters;

namespace OpenTrpg.Systems.Dnd5e.Application;

// The parts of the D&amp;D 5e contract the core does not call today (the module's own endpoints use the handlers
// directly); they delegate in the same planners and handlers. Each mutating method leaves saving to the caller.

/// <summary>Levels granted by the DM and completed by the player (<see cref="IProgressionSystem"/>).</summary>
public sealed class Dnd5eProgressionSystem(
    Dnd5eCharacterParts parts,
    LevelUpPlanner planner,
    ApplyLevelUpHandler levelUp,
    IDateTimeProvider clock) : IProgressionSystem
{
    /// <summary><paramref name="query"/>: <c>{ "classIndex": "..." }</c> (the main class when absent).</summary>
    public async Task<JsonObject> PlanAsync(CharacterRef character, JsonElement? query, CancellationToken cancellationToken = default)
    {
        var loaded = await parts.LoadAsync(character, cancellationToken);
        var classIndex = query is { ValueKind: JsonValueKind.Object } q && q.TryGetProperty("classIndex", out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
        var plan = await planner.BuildAsync(loaded, classIndex, new HashSet<string>(StringComparer.Ordinal), cancellationToken);
        return Dnd5eCharacterParts.ToJsonObject(plan.ToDto());
    }

    /// <summary><paramref name="request"/>: a <see cref="LevelUpRequest"/>. Who may level up is checked by the caller.</summary>
    public async Task ApplyAsync(CharacterRef character, JsonElement request, bool actorIsDm, CancellationToken cancellationToken = default)
    {
        var loaded = await parts.LoadAsync(character, cancellationToken);
        var body = Dnd5eCharacterParts.FromJson<LevelUpRequest>(request)
            ?? throw AppException.Validation("classIndex", "La subida de nivel no es válida.");
        await levelUp.ApplyAsync(loaded, body, clock.UtcNow, cancellationToken);
    }

    public async Task GrantAsync(IReadOnlyList<CharacterRef> characters, Guid grantedBy, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        foreach (var character in await parts.LoadManyAsync(characters, cancellationToken))
        {
            character.GrantLevelUp(grantedBy, now);
        }
    }

    public async Task RevokeAsync(IReadOnlyList<CharacterRef> characters, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        foreach (var character in await parts.LoadManyAsync(characters, cancellationToken))
        {
            character.RevokeLevelUp(now);
        }
    }
}

/// <summary>Combat tracking (<see cref="ICombatSystem"/>).</summary>
public sealed class Dnd5eCombatSystem(Dnd5eCharacterParts parts, ICharacterSheetService sheets) : ICombatSystem
{
    public async Task<JsonObject> BuildSummaryAsync(CharacterRef character, SystemSheet sheet, CancellationToken cancellationToken = default)
    {
        var loaded = await parts.LoadAsync(character, cancellationToken);
        return Dnd5eCharacterParts.ToJsonObject((await sheets.BuildSystemDetailAsync(loaded, cancellationToken)).Combat);
    }

    public async Task<JsonObject> ApplyDamageAsync(CharacterRef character, int amount, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        var loaded = await parts.LoadAsync(character, cancellationToken);
        return Dnd5eCharacterParts.ToJsonObject(DamageOutcomeDto.From(loaded.Id, loaded.ApplyDamage(amount, now)));
    }

    public async Task HealAsync(CharacterRef character, int amount, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        var loaded = await parts.LoadAsync(character, cancellationToken);
        var sheet = await sheets.CalculateAsync(loaded, cancellationToken);
        loaded.Heal(amount, sheet.HitPointsMax, now);
    }

    /// <summary>Current conditions minus the removed indexes, plus the added ones whose index is not present yet.</summary>
    public async Task SetConditionsAsync(
        CharacterRef character,
        IReadOnlyList<string> add,
        IReadOnlyList<string> remove,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        var loaded = await parts.LoadAsync(character, cancellationToken);
        var sheet = await sheets.CalculateAsync(loaded, cancellationToken);
        var removed = remove.Select(i => i.Trim()).ToHashSet(StringComparer.Ordinal);
        var result = loaded.Conditions.Where(c => !removed.Contains(c.Index)).ToList();
        foreach (var index in add.Select(i => i.Trim()).Where(i => i.Length > 0))
        {
            if (!result.Any(c => c.Index == index))
            {
                result.Add(new CharacterCondition(index));
            }
        }

        loaded.ApplyCombatUpdate(new CombatUpdate { Conditions = result }, sheet.HitPointsMax, now);
    }
}

/// <summary>Options and feats whose prerequisites no longer hold (<see cref="IChoiceSystem"/>).</summary>
public sealed class Dnd5eChoiceSystem(
    Dnd5eCharacterParts parts,
    ICharacterSheetService sheets,
    InvalidChoicesPlanner planner,
    InvalidChoicesHandler handler) : IChoiceSystem
{
    public async Task<IReadOnlyList<JsonObject>> FindInvalidAsync(CharacterRef character, CancellationToken cancellationToken = default)
    {
        var loaded = await parts.LoadAsync(character, cancellationToken);
        var sheet = await sheets.CalculateAsync(loaded, cancellationToken);
        return (await planner.FindAsync(loaded, sheet, cancellationToken))
            .Select(i => Dnd5eCharacterParts.ToJsonObject(InvalidChoicesPlanner.ToDto(i)))
            .ToList();
    }

    /// <summary><paramref name="replacements"/>: a <see cref="ReplaceInvalidChoicesRequest"/>.</summary>
    public async Task ReplaceInvalidAsync(CharacterRef character, JsonElement replacements, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        var loaded = await parts.LoadAsync(character, cancellationToken);
        var request = Dnd5eCharacterParts.FromJson<ReplaceInvalidChoicesRequest>(replacements)
            ?? throw AppException.Validation("choices", "Las sustituciones no son válidas.");
        await handler.ApplyAsync(loaded, request, now, cancellationToken);
    }
}

/// <summary>Group actions of the DM (<see cref="IPartySystem"/>).</summary>
public sealed class Dnd5ePartySystem(
    Dnd5eCharacterParts parts,
    ICharacterSheetService sheets,
    PartyRestHandler rests,
    PartyAdjustHandler adjust) : IPartySystem
{
    public async Task<JsonObject> BuildPartyAsync(IReadOnlyList<CharacterRef> characters, CancellationToken cancellationToken = default) =>
        Dnd5eCharacterParts.ToJsonObject(new PartyDto(await sheets.BuildPartyAsync(await parts.LoadManyAsync(characters, cancellationToken), cancellationToken)));

    /// <summary><paramref name="kind"/>: <c>short</c> or <c>long</c>.</summary>
    public async Task<JsonObject> RestAsync(IReadOnlyList<CharacterRef> characters, string kind, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        var loaded = await parts.LoadManyAsync(characters, cancellationToken);
        await rests.RestAsync(loaded, kind.Trim().ToLowerInvariant(), now, cancellationToken);
        return Dnd5eCharacterParts.ToJsonObject(new PartyDto(await sheets.BuildPartyAsync(loaded, cancellationToken)));
    }

    /// <summary><paramref name="adjustments"/>: a list of <see cref="PartyAdjustment"/>.</summary>
    public async Task<JsonObject> AdjustAsync(IReadOnlyList<CharacterRef> characters, JsonElement adjustments, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        var loaded = await parts.LoadManyAsync(characters, cancellationToken);
        var list = Dnd5eCharacterParts.FromJson<List<PartyAdjustment>>(adjustments)
            ?? throw AppException.Validation("adjustments", "Los ajustes no son válidos.");
        var damage = await adjust.ApplyAsync(loaded, list, now, cancellationToken);
        return Dnd5eCharacterParts.ToJsonObject(new PartyDto(await sheets.BuildPartyAsync(loaded, cancellationToken)) { Damage = damage });
    }
}
