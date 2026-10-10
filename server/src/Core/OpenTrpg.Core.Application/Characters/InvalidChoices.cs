using System.Text.Json;
using OpenTrpg.Core.Application.Abstractions;
using OpenTrpg.Core.Application.Abstractions.Persistence;
using OpenTrpg.Core.Application.Common;
using OpenTrpg.Core.Domain.Catalog;
using OpenTrpg.Core.Domain.Characters;
using FluentValidation;

namespace OpenTrpg.Core.Application.Characters;

// Phase 19: options and feats whose prerequisites no longer hold must be replaced (forced step of "Mi sesión" and of
// every level-up). GET/POST /characters/{id}/invalid-choices.

/// <summary>An option or feat that no longer meets its prerequisites (also in the character detail, <c>invalidChoices</c>).</summary>
/// <param name="ReplaceKey">Key to answer in the replacement (<c>replace.&lt;index&gt;</c>).</param>
/// <param name="ClassIndex">Class of the choice that picked it; null for an origin feat.</param>
public sealed record InvalidChoiceDto(string ReplaceKey, string? ClassIndex, string Key, int Level, string SetId, ChoiceItemDto Item, string Reason);

/// <summary>The forced replacements of a character: one choice per invalid pick (same shape as the level-up choices).</summary>
public sealed record InvalidChoicesDto(Guid CharacterId, IReadOnlyList<InvalidChoiceDto> Invalid, IReadOnlyList<LevelUpChoiceDto> Choices);

/// <summary>
/// Body of <c>POST /characters/{id}/invalid-choices</c>: one answer per replacement (<c>key</c> = <c>replace.&lt;index&gt;</c>).
/// <c>selected</c> is <c>["new-option"]</c>, or for a feat <c>{ "feat": "...", "ability": "..." }</c> (also <c>["feat"]</c>).
/// </summary>
public sealed record ReplaceInvalidChoicesRequest(IReadOnlyList<LevelUpChoiceAnswer>? Choices);

public sealed class ReplaceInvalidChoicesRequestValidator : AbstractValidator<ReplaceInvalidChoicesRequest>
{
    public ReplaceInvalidChoicesRequestValidator()
    {
        RuleFor(x => x.Choices!.Count).LessThanOrEqualTo(LevelUpRequestValidator.MaxChoices)
            .WithMessage($"No se admiten más de {LevelUpRequestValidator.MaxChoices} elecciones.")
            .When(x => x.Choices is not null)
            .OverridePropertyName("choices");
        RuleForEach(x => x.Choices).Must(c => c is not null && !string.IsNullOrWhiteSpace(c.Key))
            .WithMessage("Cada elección necesita su key.")
            .When(x => x.Choices is not null);
    }
}

/// <summary>A replacement to make: the invalid pick and the choice offered in its place.</summary>
public sealed record PlannedReplacement(InvalidChoice Invalid, PlannedChoice Choice);

/// <summary>Finds the invalid picks of a character and plans their replacements; validates and applies the answers.</summary>
public sealed class InvalidChoicesPlanner(ICatalogRepository catalog)
{
    public const string KeyPrefix = "replace.";

    public static bool IsReplacement(string key) => key.StartsWith(KeyPrefix, StringComparison.Ordinal);

    public static InvalidChoiceDto ToDto(InvalidChoice invalid) => new(
        KeyPrefix + invalid.Item.Index, invalid.ClassIndex, invalid.Key, invalid.Level, invalid.SetId, ChoiceItemDto.From(invalid.Item), invalid.Reason);

    /// <summary>The invalid picks of the character (catalog options of its picks loaded here).</summary>
    public async Task<IReadOnlyList<InvalidChoice>> FindAsync(Dnd5eCharacter character, CharacterSheet sheet, CancellationToken cancellationToken = default)
    {
        var indexes = ChoiceEffects.OptionIndexes(character).Distinct(StringComparer.Ordinal).ToList();
        if (indexes.Count == 0)
        {
            return [];
        }

        var options = (await catalog.ListOptionsByIndexAsync(indexes, cancellationToken)).ToDictionary(o => o.Index, StringComparer.Ordinal);
        return ChoiceValidity.Find(character, sheet, options.GetValueOrDefault);
    }

    public async Task<IReadOnlyList<PlannedReplacement>> PlanAsync(Dnd5eCharacter character, CharacterSheet sheet, CancellationToken cancellationToken = default)
    {
        var invalid = await FindAsync(character, sheet, cancellationToken);
        if (invalid.Count == 0)
        {
            return [];
        }

        var setIds = invalid.Select(i => i.SetId).Append(OptionSets.PactBoons).ToHashSet(StringComparer.Ordinal);
        var all = await catalog.ListOptionsBySetAsync(setIds, cancellationToken);
        var byIndex = all.GroupBy(o => o.Index, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        var invalidIndexes = invalid.Select(i => i.Item.Index).ToHashSet(StringComparer.Ordinal);
        var picks = character.ActivePicks().Select(p => p.Item.Index).Where(i => !invalidIndexes.Contains(i)).ToHashSet(StringComparer.Ordinal);

        return invalid.Select(i =>
            {
                var options = all
                    .Where(o => o.SetId == i.SetId && !picks.Contains(o.Index) && !invalidIndexes.Contains(o.Index))
                    .OrderBy(o => o.Name, StringComparer.Ordinal)
                    .Select(o =>
                    {
                        var reason = ChoiceValidity.WhyNot(o, character, sheet, i.ClassIndex, picks, byIndex.GetValueOrDefault);
                        return new PlannedOption(o.Index, o.Name, o.Description, o.PrerequisitesText, reason is null, reason, null, o, []);
                    })
                    .ToList();
                var rule = new LevelChoiceRule
                {
                    Id = $"replace/{i.ClassIndex ?? "-"}/{i.Key}/{i.Item.Index}",
                    ClassIndex = i.ClassIndex ?? string.Empty,
                    Level = i.Level,
                    Key = KeyPrefix + i.Item.Index,
                    Name = $"Sustituir «{i.Item.Name}»",
                    Kind = i.IsFeat ? LevelChoiceKind.AsiOrFeat : LevelChoiceKind.OptionSet,
                    SetId = i.SetId,
                    Choose = 1,
                    Replaces = true,
                    Note = $"Ya no cumples sus requisitos: {i.Reason} Elige otra opción.",
                };
                return new PlannedReplacement(i, new PlannedChoice(rule, options.Any(o => o.Eligible) ? 1 : 0, false, options, [i.Item]));
            })
            .ToList();
    }

    /// <summary>
    /// Validates the answers to the replacements (every one with a required pick must be answered) and records them:
    /// the invalid pick is swapped out in its own choice (class and key) for the new one.
    /// Answers of other keys are ignored (the caller checks them). Returns the replaced option indexes.
    /// </summary>
    public async Task<IReadOnlyList<string>> ApplyAsync(
        Dnd5eCharacter character,
        IReadOnlyList<PlannedReplacement> replacements,
        IReadOnlyList<LevelUpChoiceAnswer> answers,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        var resolved = new List<(InvalidChoice Invalid, ChoiceItem? Selected, string? Ability)>();
        foreach (var replacement in replacements)
        {
            var key = replacement.Choice.Rule.Key;
            var answer = answers.FirstOrDefault(a => a?.Key?.Trim() == key);
            var (index, ability) = answer is null ? (null, null) : Parse(replacement, answer.Selected);
            if (index is null)
            {
                if (replacement.Choice.Required > 0)
                {
                    throw AppException.Validation("choices", $"Falta la elección «{replacement.Choice.Rule.Name}».");
                }

                resolved.Add((replacement.Invalid, null, null));
                continue;
            }

            var option = replacement.Choice.Option(index)
                ?? throw AppException.Validation("choices", $"«{index}» no es una opción de «{replacement.Choice.Rule.Name}».");
            if (!option.Eligible)
            {
                throw AppException.Validation("choices", $"«{option.Name}» no cumple los requisitos: {option.Reason}");
            }

            string? raised = null;
            if (replacement.Invalid.IsFeat && option.Definition?.AbilityIncrease is { } increase)
            {
                raised = ability ?? (increase.From.Count == 1 ? increase.From[0] : null);
                if (raised is null || !Abilities.IsValid(raised) || (increase.From.Count > 0 && !increase.From.Contains(raised, StringComparer.Ordinal)))
                {
                    throw AppException.Validation("choices", $"«{option.Name}»: elige la característica que sube ({(increase.From.Count == 0 ? "cualquiera" : string.Join(", ", increase.From))}).");
                }
            }

            resolved.Add((replacement.Invalid, new ChoiceItem(option.Index, option.Name), raised));
        }

        foreach (var (invalid, selected, ability) in resolved)
        {
            if (invalid.ClassIndex is null)
            {
                // Origin feat: the origin choice is answered again.
                if (selected is null)
                {
                    character.RemoveOriginChoices(k => k == invalid.Key);
                }
                else
                {
                    var current = character.OriginChoice(invalid.Key)?.Selection;
                    character.RecordOriginChoice(
                        invalid.Key,
                        new ChoiceSelection { Kind = OriginChoiceKeys.FeatKind, Name = current?.Name ?? "Dote", SetId = OptionSets.Feats, Feat = selected, Ability = ability },
                        now);
                }

                continue;
            }

            var level = character.Classes.FirstOrDefault(c => c.ClassIndex == invalid.ClassIndex)?.Level ?? invalid.Level;
            var selection = invalid.IsFeat
                ? new ChoiceSelection { Kind = nameof(LevelChoiceKind.AsiOrFeat), Name = "Sustitución", SetId = OptionSets.Feats, Feat = selected, Ability = ability, Replaced = [invalid.Item] }
                : new ChoiceSelection { Kind = invalid.Kind, Name = "Sustitución", SetId = invalid.SetId, Selected = selected is null ? [] : [selected], Replaced = [invalid.Item] };
            character.RecordChoice(Math.Max(1, level), invalid.ClassIndex, invalid.Key, selection, now);
        }

        var replaced = resolved.Select(r => r.Invalid.Item.Index).ToList();
        await ChoiceGrants.ApplyAsync(catalog, character, replaced, cancellationToken);
        return replaced;
    }

    private static (string? Index, string? Ability) Parse(PlannedReplacement replacement, JsonElement selected)
    {
        switch (selected.ValueKind)
        {
            case JsonValueKind.Undefined or JsonValueKind.Null:
                return (null, null);
            case JsonValueKind.Array when selected.GetArrayLength() == 0:
                return (null, null);
            case JsonValueKind.Array when selected.GetArrayLength() == 1 && selected[0].ValueKind == JsonValueKind.String:
                return (LevelUpPlanner.NormalizeIndex(selected[0].GetString() ?? string.Empty), null);
            case JsonValueKind.Object when replacement.Invalid.IsFeat:
                string? feat = null;
                string? ability = null;
                foreach (var property in selected.EnumerateObject())
                {
                    switch (property.Name.ToLowerInvariant())
                    {
                        case "feat" when property.Value.ValueKind == JsonValueKind.String:
                            feat = property.Value.GetString()?.Trim();
                            break;
                        case "ability" when property.Value.ValueKind is JsonValueKind.String or JsonValueKind.Null:
                            ability = property.Value.GetString()?.Trim().ToLowerInvariant();
                            break;
                        default:
                            throw AppException.Validation("choices", $"La elección «{replacement.Choice.Rule.Name}» tiene un campo desconocido: {property.Name}.");
                    }
                }

                return (string.IsNullOrEmpty(feat) ? null : feat, string.IsNullOrEmpty(ability) ? null : ability);
            default:
                throw AppException.Validation("choices", $"«{replacement.Choice.Rule.Name}»: envía una sola opción en selected.");
        }
    }
}

/// <summary>Forced replacement of invalid options and feats: owner or DM, without approval.</summary>
public sealed class InvalidChoicesHandler(
    Dnd5eCharacterLoader loader,
    InvalidChoicesPlanner planner,
    ICharacterSheetService sheets,
    IUnitOfWork unitOfWork,
    ICampaignNotifier notifier,
    IDateTimeProvider clock)
{
    public async Task<InvalidChoicesDto> GetAsync(Guid currentUserId, Guid characterId, CancellationToken cancellationToken = default)
    {
        var loaded = await loader.LoadAsync(characterId, currentUserId, cancellationToken);
        loaded.Character.EnsureCanTrack(currentUserId, loaded.IsDm);
        var sheet = await sheets.CalculateAsync(loaded.Character, cancellationToken);
        var plan = await planner.PlanAsync(loaded.Character, sheet, cancellationToken);
        return new InvalidChoicesDto(
            loaded.Character.Id,
            plan.Select(p => InvalidChoicesPlanner.ToDto(p.Invalid)).ToList(),
            plan.Select(p => p.Choice.ToDto()).ToList());
    }

    public async Task<CharacterDetailDto> ReplaceAsync(Guid currentUserId, Guid characterId, ReplaceInvalidChoicesRequest request, CancellationToken cancellationToken = default)
    {
        var loaded = await loader.LoadAsync(characterId, currentUserId, cancellationToken);
        var character = loaded.Character;
        character.EnsureCanTrack(currentUserId, loaded.IsDm);
        var now = clock.UtcNow;
        await ApplyAsync(character, request, now, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await notifier.CharacterUpdatedAsync(character.CampaignId, character.Id, now, cancellationToken);
        return await sheets.BuildDetailAsync(character, cancellationToken);
    }

    /// <summary>Replaces the invalid choices with the answers and recalculates the sheet; the caller saves.</summary>
    public async Task ApplyAsync(Dnd5eCharacter character, ReplaceInvalidChoicesRequest request, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var sheet = await sheets.CalculateAsync(character, cancellationToken);
        var plan = await planner.PlanAsync(character, sheet, cancellationToken);
        if (plan.Count == 0)
        {
            throw AppException.Conflict("No hay elecciones que sustituir.");
        }

        var answers = request.Choices ?? [];
        if (answers.FirstOrDefault(a => plan.All(p => p.Choice.Rule.Key != a.Key?.Trim())) is { } unknown)
        {
            throw AppException.Validation("choices", $"La elección '{unknown.Key}' no corresponde a ninguna sustitución pendiente.");
        }

        await planner.ApplyAsync(character, plan, answers, now, cancellationToken);
        await sheets.RecalculateAsync(character, cancellationToken);
    }
}
