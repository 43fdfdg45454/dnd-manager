using System.Text.Json;
using OpenTrpg.Core.Application.Abstractions;
using OpenTrpg.Core.Application.Abstractions.Persistence;
using OpenTrpg.Core.Application.Common;
using OpenTrpg.Core.Domain.Catalog;
using OpenTrpg.Core.Domain.Characters;
using OpenTrpg.Core.Application;
using OpenTrpg.Core.Application.Characters;
using OpenTrpg.Systems.Dnd5e.Application;
using OpenTrpg.Systems.Dnd5e.Application.Abstractions;
using OpenTrpg.Systems.Dnd5e.Application.Abstractions.Persistence;
using OpenTrpg.Systems.Dnd5e.Application.Characters;
using OpenTrpg.Systems.Dnd5e.Domain.Catalog;
using OpenTrpg.Systems.Dnd5e.Domain.Characters;

namespace OpenTrpg.Systems.Dnd5e.Application.Characters;

/// <summary>
/// The level-up plan of a character for a class (<c>?classIndex=</c>, the main class by default). The owner
/// needs a level granted by a DM (409 otherwise); DMs can always look. Other players get 403.
/// </summary>
public sealed class GetLevelUpPlanHandler(Dnd5eCharacterLoader characters, LevelUpPlanner planner)
{
    public async Task<LevelUpPlanDto> HandleAsync(Guid currentUserId, Guid characterId, string? classIndex, CancellationToken cancellationToken = default)
    {
        var loaded = await characters.LoadAsync(characterId, currentUserId, cancellationToken);
        LevelUpRules.EnsureCanLevelUp(loaded, currentUserId);
        var plan = await planner.BuildAsync(loaded.Character, classIndex, new HashSet<string>(StringComparer.Ordinal), cancellationToken);
        return plan.ToDto();
    }
}

/// <summary>
/// Applies a level-up: validates the class (multiclassing prerequisites), the hit points rolled (1..die) and the
/// answers to every choice of the plan, then in one transaction adds the class level, records the choices and the
/// roll, applies their effects (subclass, expertise, proficiencies, spells, grants), recalculates the sheet (the
/// hit points grow by the roll + Con, at least 1) and clears the pending level. Owner with a granted level, or DMs.
/// </summary>
public sealed class ApplyLevelUpHandler(
    Dnd5eCharacterLoader characters,
    LevelUpPlanner planner,
    InvalidChoicesPlanner invalidChoices,
    ICatalogRepository catalog,
    ICharacterSheetService sheets,
    IUnitOfWork unitOfWork,
    ICampaignNotifier notifier,
    IDateTimeProvider clock)
{
    public async Task<CharacterDetailDto> HandleAsync(Guid currentUserId, Guid characterId, LevelUpRequest request, CancellationToken cancellationToken = default)
    {
        var loaded = await characters.LoadAsync(characterId, currentUserId, cancellationToken);
        LevelUpRules.EnsureCanLevelUp(loaded, currentUserId);
        var character = loaded.Character;
        var now = clock.UtcNow;
        await ApplyAsync(character, request, now, cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await notifier.CharacterUpdatedAsync(character.CampaignId, character.Id, now, cancellationToken);
        await notifier.NotifyAsync(new CampaignEvent(Dnd5eEventTypes.LevelUpCompleted, character.CampaignId, character.Id, null, now), cancellationToken);
        return await sheets.BuildDetailAsync(character, cancellationToken);
    }

    /// <summary>Applies the level-up (checked against the plan) and recalculates the sheet; the caller saves.</summary>
    public async Task ApplyAsync(Dnd5eCharacter character, LevelUpRequest request, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var answers = ParseAnswers(request.Choices ?? []);
        var pending = answers.Values
            .SelectMany(a => a.Feat is null ? a.Selected : [.. a.Selected, a.Feat])
            .ToHashSet(StringComparer.Ordinal);
        var pendingChoices = answers.Values.ToDictionary(
            a => a.Key,
            a => (IReadOnlyList<string>)(a.Feat is null ? a.Selected : [.. a.Selected, a.Feat]),
            StringComparer.Ordinal);
        var plan = await planner.BuildAsync(character, request.ClassIndex, pending, cancellationToken, pendingChoices);

        if (!plan.SelectedClass.Allowed)
        {
            throw AppException.Validation("classIndex", plan.SelectedClass.Reason ?? "No puedes subir de nivel en esta clase.");
        }

        if (request.HitPointsRolled < 1 || request.HitPointsRolled > plan.Class.HitDie)
        {
            throw AppException.Validation("hitPointsRolled", $"La tirada de puntos de golpe debe estar entre 1 y {plan.Class.HitDie} (d{plan.Class.HitDie}).");
        }

        var resolved = Resolve(plan, answers);

        // ---- Apply (one SaveChanges: one transaction) -------------------------------------------------
        var oldMax = plan.Sheet.HitPointsMax;
        var classIndex = plan.Class.Index;

        // Invalid options and feats are swapped first, at the class levels they had.
        if (plan.Replacements.Count > 0)
        {
            await invalidChoices.ApplyAsync(character, plan.Replacements, request.Choices ?? [], now, cancellationToken);
        }

        var entry = character.AdvanceClass(classIndex, now);
        if (plan.IsNew)
        {
            foreach (var (type, key) in MulticlassRules.ProficienciesFor(classIndex, plan.Class.Multiclassing))
            {
                character.AddProficiency(type, key, ProficiencySource.Class);
            }
        }

        character.RecordChoice(
            entry.Level,
            classIndex,
            CharacterChoice.HitPointsKey,
            new ChoiceSelection { Kind = ChoiceSelection.HitPointsKind, Name = "Puntos de golpe", Roll = request.HitPointsRolled },
            now);
        if (character.HpMode == HpMode.Manual)
        {
            character.RaiseHitPointsMaxOverride(Math.Max(1, request.HitPointsRolled + plan.ConModifier));
        }

        // Expertise goes last: it doubles skills picked or granted (subclass, options) in this same level-up.
        var replacedOptions = new List<string>();
        foreach (var (choice, selection) in resolved)
        {
            character.RecordChoice(entry.Level, classIndex, choice.Rule.Key, selection, now);
            if (choice.Rule.Kind != LevelChoiceKind.Expertise)
            {
                ApplyEffects(character, classIndex, choice, selection, replacedOptions, now);
            }
        }

        await ChoiceGrants.ApplyAsync(catalog, character, replacedOptions, cancellationToken);
        foreach (var (choice, selection) in resolved.Where(r => r.Choice.Rule.Kind == LevelChoiceKind.Expertise))
        {
            ApplyEffects(character, classIndex, choice, selection, replacedOptions, now);
        }

        var sheet = await sheets.RecalculateAsync(character, cancellationToken);
        character.GainHitPoints(sheet.HitPointsMax - oldMax, sheet.HitPointsMax, now);

        // A level in a class that prepares spells (the first one with slots included, e.g. paladin 2) lets the
        // character change its preparation: the app forces the preparation screen.
        if (sheet.PreparingClasses.Any(s => s.ClassIndex == classIndex))
        {
            character.RequireSpellPreparation(SpellPreparationReason.LevelUp, now);
        }
    }

    // ---- Validation ----------------------------------------------------------------------------------

    /// <summary>An answer as sent: picks (normalized indexes or texts), replaced picks, or an ASI/feat object.</summary>
    private sealed record Answer(string Key, List<string> Selected, List<string> Replaced, Dictionary<string, int>? Asi, string? Feat, string? Ability);

    private static Dictionary<string, Answer> ParseAnswers(IReadOnlyList<LevelUpChoiceAnswer> choices)
    {
        var result = new Dictionary<string, Answer>(StringComparer.Ordinal);
        foreach (var choice in choices)
        {
            var key = choice?.Key?.Trim();
            if (string.IsNullOrEmpty(key))
            {
                throw AppException.Validation("choices", "Cada elección necesita su key.");
            }

            if (result.ContainsKey(key))
            {
                throw AppException.Validation("choices", $"La elección '{key}' está repetida.");
            }

            var replaced = (choice!.Replaced ?? []).Select(r => LevelUpPlanner.NormalizeIndex(r ?? string.Empty)).ToList();
            var selected = choice.Selected;
            switch (selected.ValueKind)
            {
                case JsonValueKind.Undefined or JsonValueKind.Null:
                    result[key] = new Answer(key, [], replaced, null, null, null);
                    break;
                case JsonValueKind.Array:
                    var picks = new List<string>();
                    foreach (var element in selected.EnumerateArray())
                    {
                        if (element.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(element.GetString()))
                        {
                            throw AppException.Validation("choices", $"La elección '{key}' solo admite textos en selected.");
                        }

                        picks.Add(LevelUpPlanner.NormalizeIndex(element.GetString()!));
                    }

                    result[key] = new Answer(key, picks, replaced, null, null, null);
                    break;
                case JsonValueKind.Object:
                    result[key] = ParseImprovement(key, selected, replaced);
                    break;
                default:
                    throw AppException.Validation("choices", $"La elección '{key}' tiene un selected no válido.");
            }
        }

        return result;
    }

    /// <summary><c>{ "asi": { "str": 1, "dex": 1 } }</c> or <c>{ "feat": "grappler", "ability": "str" }</c>.</summary>
    private static Answer ParseImprovement(string key, JsonElement selected, List<string> replaced)
    {
        Dictionary<string, int>? asi = null;
        string? feat = null;
        string? ability = null;
        foreach (var property in selected.EnumerateObject())
        {
            switch (property.Name.ToLowerInvariant())
            {
                case "asi" when property.Value.ValueKind == JsonValueKind.Object:
                    asi = new Dictionary<string, int>(StringComparer.Ordinal);
                    foreach (var score in property.Value.EnumerateObject())
                    {
                        if (score.Value.ValueKind != JsonValueKind.Number || !score.Value.TryGetInt32(out var amount))
                        {
                            throw AppException.Validation("choices", "La mejora de característica solo admite números.");
                        }

                        var name = score.Name.Trim().ToLowerInvariant();
                        asi[name] = asi.GetValueOrDefault(name) + amount;
                    }

                    break;
                case "feat" when property.Value.ValueKind == JsonValueKind.String:
                    feat = property.Value.GetString()?.Trim();
                    break;
                case "ability" when property.Value.ValueKind is JsonValueKind.String or JsonValueKind.Null:
                    ability = property.Value.GetString()?.Trim().ToLowerInvariant();
                    break;
                default:
                    throw AppException.Validation("choices", $"La elección '{key}' tiene un campo desconocido: {property.Name}.");
            }
        }

        return new Answer(key, [], replaced, asi, string.IsNullOrEmpty(feat) ? null : feat, string.IsNullOrEmpty(ability) ? null : ability);
    }

    /// <summary>Validates every answer against the plan and turns it into the selection to record.</summary>
    private static List<(PlannedChoice Choice, ChoiceSelection Selection)> Resolve(LevelUpPlan plan, Dictionary<string, Answer> answers)
    {
        var subclassChoice = plan.Choices.FirstOrDefault(c => c.Rule.Kind == LevelChoiceKind.Subclass);
        var subclass = plan.Entry?.SubclassIndex;
        if (subclassChoice is not null && answers.TryGetValue(subclassChoice.Rule.Key, out var subclassAnswer) && subclassAnswer.Selected.Count == 1)
        {
            subclass = subclassAnswer.Selected[0];
        }

        var result = new List<(PlannedChoice, ChoiceSelection)>();
        var used = plan.Replacements.Select(r => r.Choice.Rule.Key).ToHashSet(StringComparer.Ordinal);
        foreach (var choice in plan.Choices.Where(c => !InvalidChoicesPlanner.IsReplacement(c.Rule.Key)))
        {
            var rule = choice.Rule;
            if (rule.SubclassIndex is { } ruleSubclass && ruleSubclass != subclass)
            {
                continue;
            }

            if (!answers.TryGetValue(rule.Key, out var answer))
            {
                if (choice.Required > 0)
                {
                    throw AppException.Validation("choices", $"Falta la elección «{rule.Name}».");
                }

                continue;
            }

            used.Add(rule.Key);
            var selection = rule.Kind == LevelChoiceKind.AsiOrFeat
                ? ResolveImprovement(plan, choice, answer)
                : ResolvePicks(choice, answer);
            if (selection is not null)
            {
                result.Add((choice, selection));
            }
        }

        if (answers.Keys.FirstOrDefault(k => !used.Contains(k)) is { } unknown)
        {
            throw AppException.Validation("choices", $"La elección '{unknown}' no corresponde a esta subida de nivel.");
        }

        return result;
    }

    private static ChoiceSelection? ResolvePicks(PlannedChoice choice, Answer answer)
    {
        var rule = choice.Rule;
        if (answer.Asi is not null || answer.Feat is not null)
        {
            throw AppException.Validation("choices", $"La elección «{rule.Name}» espera una lista en selected.");
        }

        if (answer.Replaced.Count > 0 && !rule.Replaces)
        {
            throw AppException.Validation("choices", $"La elección «{rule.Name}» no permite sustituir.");
        }

        if (answer.Replaced.Count > 1)
        {
            throw AppException.Validation("choices", $"En «{rule.Name}» solo se puede sustituir una elección por nivel.");
        }

        var replaced = new List<ChoiceItem>();
        foreach (var index in answer.Replaced)
        {
            replaced.Add(choice.Known.FirstOrDefault(k => k.Index == index)
                ?? throw AppException.Validation("choices", $"«{index}» no se puede sustituir en «{rule.Name}»: no lo tienes."));
        }

        if (answer.Selected.Distinct(StringComparer.Ordinal).Count() != answer.Selected.Count)
        {
            throw AppException.Validation("choices", $"La elección «{rule.Name}» repite una opción.");
        }

        var expected = choice.Required + replaced.Count;
        if (answer.Selected.Count != expected)
        {
            throw AppException.Validation("choices", $"La elección «{rule.Name}» necesita exactamente {expected} {(expected == 1 ? "opción" : "opciones")}.");
        }

        var selected = new List<ChoiceItem>();
        foreach (var index in answer.Selected)
        {
            if (choice.FreeText)
            {
                if (index.Length > Dnd5eCharacter.IndexMaxLength)
                {
                    throw AppException.Validation("choices", $"«{rule.Name}»: cada valor admite como máximo {Dnd5eCharacter.IndexMaxLength} caracteres.");
                }

                selected.Add(new ChoiceItem(index, index));
                continue;
            }

            var option = choice.Option(index)
                ?? throw AppException.Validation("choices", $"«{index}» no es una opción de «{rule.Name}».");
            if (!option.Eligible)
            {
                throw AppException.Validation("choices", $"«{option.Name}» no cumple los requisitos: {option.Reason}");
            }

            if (replaced.Any(r => r.Index == index))
            {
                throw AppException.Validation("choices", $"«{option.Name}» no puede sustituirse por sí misma.");
            }

            selected.Add(new ChoiceItem(option.Index, option.Name));
        }

        return selected.Count == 0 && replaced.Count == 0
            ? null
            : new ChoiceSelection { Kind = rule.Kind.ToString(), Name = rule.Name, SetId = rule.SetId, Selected = selected, Replaced = replaced };
    }

    /// <summary>Ability Score Improvement (+2 to one ability or +1 to two, never above 20) or an eligible feat.</summary>
    private static ChoiceSelection ResolveImprovement(LevelUpPlan plan, PlannedChoice choice, Answer answer)
    {
        var rule = choice.Rule;
        int Natural(string ability) => plan.Sheet.Breakdowns.TryGetValue($"ability.{ability}", out var breakdown)
            ? breakdown.Parts.Where(p => p.Source is not (BreakdownSources.Item or BreakdownSources.Override)).Sum(p => p.Value)
            : plan.Sheet.Abilities[ability].Score;

        void EnsureLimit(string ability, int amount)
        {
            if (Natural(ability) + amount > SheetCalculator.ImprovementMaxScore)
            {
                throw AppException.Validation("choices", $"La mejora no puede subir {BreakdownLabels.Ability(ability)} por encima de {SheetCalculator.ImprovementMaxScore}.");
            }
        }

        if (answer.Selected.Count > 0 || answer.Replaced.Count > 0 || (answer.Asi is null) == (answer.Feat is null))
        {
            throw AppException.Validation("choices", $"«{rule.Name}»: envía {{ \"asi\": {{...}} }} o {{ \"feat\": \"...\" }}.");
        }

        if (answer.Asi is { } asi)
        {
            var increases = asi.Where(a => a.Value != 0).ToDictionary(a => a.Key, a => a.Value, StringComparer.Ordinal);
            if (increases.Keys.Any(a => !Abilities.IsValid(a)) || increases.Values.Any(v => v is < 1 or > 2) || increases.Values.Sum() != 2)
            {
                throw AppException.Validation("choices", "La mejora de característica es +2 a una característica o +1 a dos.");
            }

            foreach (var (ability, amount) in increases)
            {
                EnsureLimit(ability, amount);
            }

            return new ChoiceSelection { Kind = rule.Kind.ToString(), Name = rule.Name, Asi = increases };
        }

        var feat = choice.Option(answer.Feat!)
            ?? throw AppException.Validation("choices", $"«{answer.Feat}» no es una dote disponible.");
        if (!feat.Eligible)
        {
            throw AppException.Validation("choices", $"«{feat.Name}» no cumple los requisitos: {feat.Reason}");
        }

        string? raised = null;
        if (feat.Definition?.AbilityIncrease is { } increase)
        {
            raised = answer.Ability ?? (increase.From.Count == 1 ? increase.From[0] : null);
            if (raised is null || !Abilities.IsValid(raised) || (increase.From.Count > 0 && !increase.From.Contains(raised, StringComparer.Ordinal)))
            {
                throw AppException.Validation("choices", $"«{feat.Name}»: elige la característica que sube ({(increase.From.Count == 0 ? "cualquiera" : string.Join(", ", increase.From))}).");
            }

            EnsureLimit(raised, increase.Amount);
        }
        else if (answer.Ability is not null)
        {
            throw AppException.Validation("choices", $"«{feat.Name}» no sube ninguna característica.");
        }

        return new ChoiceSelection
        {
            Kind = rule.Kind.ToString(),
            Name = rule.Name,
            SetId = OptionSets.Feats,
            Feat = new ChoiceItem(feat.Index, feat.Name),
            Ability = raised,
        };
    }

    // ---- Effects -------------------------------------------------------------------------------------

    /// <summary>Immediate effects of a choice; option effects (modifiers, increases, resources) live in the sheet.</summary>
    private static void ApplyEffects(Dnd5eCharacter character, string classIndex, PlannedChoice choice, ChoiceSelection selection, List<string> replacedOptions, DateTimeOffset now)
    {
        switch (choice.Rule.Kind)
        {
            case LevelChoiceKind.Subclass when selection.Selected.Count == 1:
                character.SetSubclass(classIndex, selection.Selected[0].Index, now);
                break;
            case LevelChoiceKind.Expertise:
                foreach (var item in selection.Selected)
                {
                    character.GrantExpertise(item.Index == "thieves-tools" ? ProficiencyType.Tool : ProficiencyType.Skill, item.Index);
                }

                break;
            case LevelChoiceKind.Skill:
                foreach (var item in selection.Selected)
                {
                    character.AddProficiency(ProficiencyType.Skill, item.Index, ProficiencySource.Class);
                }

                break;
            case LevelChoiceKind.Language or LevelChoiceKind.Tool:
                var type = choice.Rule.Kind == LevelChoiceKind.Language ? ProficiencyType.Language : ProficiencyType.Tool;
                foreach (var item in selection.Selected)
                {
                    character.AddProficiency(type, item.Index, ProficiencySource.Class);
                }

                break;
            case LevelChoiceKind.CantripsKnown or LevelChoiceKind.SpellsKnown or LevelChoiceKind.SpellbookSpells:
                foreach (var item in selection.Replaced)
                {
                    character.RemoveSpell(item.Index, classIndex);
                }

                // Wizard spellbook spells are known, not prepared.
                var prepared = choice.Rule.Kind != LevelChoiceKind.SpellbookSpells;
                foreach (var item in selection.Selected)
                {
                    character.AddSpell(item.Index, classIndex, prepared, alwaysPrepared: false);
                }

                break;
            case LevelChoiceKind.OptionSet or LevelChoiceKind.Custom:
                replacedOptions.AddRange(selection.Replaced.Select(r => r.Index));
                break;
        }
    }
}

/// <summary>Who may level a character up.</summary>
public static class LevelUpRules
{
    /// <summary>The owner (with a level granted by a DM) and DMs; 403 for other players, 409 without a grant or at level 20.</summary>
    public static void EnsureCanLevelUp(LoadedDnd5eCharacter loaded, Guid actorUserId)
    {
        ArgumentNullException.ThrowIfNull(loaded);
        if (!loaded.Character.CanViewSheet(actorUserId, loaded.IsDm))
        {
            throw AppException.Forbidden("Solo el dueño del personaje o un DM pueden subirlo de nivel.");
        }

        loaded.Character.LevelUpTarget(loaded.IsDm);
    }
}
