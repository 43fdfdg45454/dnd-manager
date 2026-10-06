using System.Text.Json;
using Dnd.Application.Abstractions;
using Dnd.Application.Abstractions.Persistence;
using Dnd.Application.Common;
using Dnd.Domain.Catalog;
using Dnd.Domain.Characters;
using FluentValidation;

namespace Dnd.Application.Characters;

// Phase 19: decisions of the race, subrace and background (GET/PUT /characters/{id}/origin-choices).

/// <summary>
/// One decision of the race, subrace or background of a character. <see cref="Required"/> picks are needed before the
/// draft can be activated (0 for optional ones: languages, which the creation wizard already asks for in its own step).
/// <see cref="Selected"/> is the current answer (empty when not answered); feats also carry <see cref="Feat"/> and the
/// <see cref="Ability"/> they raise.
/// </summary>
/// <param name="Key">"race.abilityBonuses", "race.skills", "race.languages", "race.tools", "race.cantrip", "race.feat",
/// "race.trait.&lt;key&gt;"; subrace ones start with "race.subrace."; background ones with "background.".</param>
/// <param name="Kind">AbilityBonus, Skill, Language, Tool, Cantrip, Feat or TraitOption.</param>
/// <param name="Source">"race", "subrace" or "background".</param>
/// <param name="Amount">Bonus of each pick of an AbilityBonus choice (+1).</param>
/// <param name="FreeText">No closed list: any text (languages or tools of any kind).</param>
public sealed record OriginChoiceDto(
    string Key,
    string Name,
    string Kind,
    string Source,
    int Choose,
    int Required,
    int? Amount,
    bool FreeText,
    string Note,
    IReadOnlyList<LevelUpOptionDto> Options,
    IReadOnlyList<ChoiceItemDto> Selected,
    ChoiceItemDto? Feat,
    string? Ability);

/// <param name="Complete">Every required choice is answered (the draft can be submitted or activated).</param>
public sealed record OriginChoicesDto(Guid CharacterId, bool Complete, IReadOnlyList<OriginChoiceDto> Choices);

/// <summary>
/// Body of <c>PUT /characters/{id}/origin-choices</c>: answers to some choices (the others keep theirs). <c>selected</c>
/// is an array of indexes (abilities, skills, language names, tools, spells, trait options) or, for a feat,
/// <c>{ "feat": "grappler", "ability": "str" }</c>; null or an empty array removes the answer.
/// </summary>
public sealed record SaveOriginChoicesRequest(IReadOnlyList<LevelUpChoiceAnswer>? Choices);

public sealed class SaveOriginChoicesRequestValidator : AbstractValidator<SaveOriginChoicesRequest>
{
    public SaveOriginChoicesRequestValidator()
    {
        RuleFor(x => x.Choices).NotNull().WithMessage("Indica las elecciones.");
        RuleFor(x => x.Choices!.Count).LessThanOrEqualTo(LevelUpRequestValidator.MaxChoices)
            .WithMessage($"No se admiten más de {LevelUpRequestValidator.MaxChoices} elecciones.")
            .When(x => x.Choices is not null)
            .OverridePropertyName("choices");
        RuleForEach(x => x.Choices).Must(c => c is not null && !string.IsNullOrWhiteSpace(c.Key))
            .WithMessage("Cada elección necesita su key.")
            .When(x => x.Choices is not null);
    }
}

/// <summary>A planned origin choice with its catalog options (feat definitions for the feat choice).</summary>
public sealed record PlannedOriginChoice(
    string Key,
    string Name,
    string Kind,
    string Source,
    int Choose,
    int Required,
    int? Amount,
    bool FreeText,
    string Note,
    IReadOnlyList<PlannedOption> Options)
{
    public PlannedOption? Option(string index) => Options.FirstOrDefault(o => o.Index == index);
}

/// <summary>
/// Builds the origin choices of a character from its race, subrace and background (<see cref="RaceChoices"/>) and checks
/// that the required ones are answered.
/// </summary>
public sealed class OriginChoicesPlanner(ICatalogRepository catalog, ICharacterSheetService sheets)
{
    public const string IncompleteCode = "origin-choices-incomplete";

    public async Task<IReadOnlyList<PlannedOriginChoice>> PlanAsync(Character character, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(character);
        var race = character.RaceIndex is null ? null : await catalog.GetRaceAsync(character.RaceIndex, cancellationToken);
        var subrace = character.SubraceIndex is null ? null : (await catalog.ListSubracesByIndexAsync([character.SubraceIndex], cancellationToken)).FirstOrDefault();
        var background = character.BackgroundIndex is null ? null : (await catalog.ListBackgroundsByIndexAsync([character.BackgroundIndex], cancellationToken)).FirstOrDefault();

        var sources = new List<(string Prefix, string Source, RaceChoices Choices)>();
        if (race is not null)
        {
            sources.Add((OriginChoiceKeys.RacePrefix, BreakdownSources.Race, race.Choices));
        }

        if (subrace is not null && subrace.RaceIndex == character.RaceIndex)
        {
            sources.Add((OriginChoiceKeys.SubracePrefix, BreakdownSources.Subrace, subrace.Choices));
        }

        if (background is not null)
        {
            sources.Add((OriginChoiceKeys.BackgroundPrefix, BreakdownSources.Background, background.Choices));
        }

        if (sources.All(s => s.Choices.IsEmpty))
        {
            return [];
        }

        var skills = await catalog.ListSkillsAsync(cancellationToken);
        var spells = sources.Any(s => s.Choices.Cantrip is not null) ? await catalog.ListAllSpellsAsync(cancellationToken) : [];
        var feats = sources.Any(s => s.Choices.Feats is not null) ? await catalog.ListOptionsBySetAsync([OptionSets.Feats], cancellationToken) : [];
        var sheet = feats.Count > 0 ? await sheets.CalculateAsync(character, cancellationToken) : null;

        var result = new List<PlannedOriginChoice>();
        foreach (var (prefix, source, choices) in sources)
        {
            result.AddRange(Plan(character, prefix, source, choices, skills, spells, feats, sheet));
        }

        return result;
    }

    /// <summary>The required choices without a complete answer (names), empty when everything is answered.</summary>
    public static IReadOnlyList<string> Missing(Character character, IReadOnlyList<PlannedOriginChoice> plan)
    {
        ArgumentNullException.ThrowIfNull(character);
        ArgumentNullException.ThrowIfNull(plan);
        return plan
            .Where(c => c.Required > 0 && Answered(character.OriginChoice(c.Key)) < c.Required)
            .Select(c => c.Name)
            .ToList();
    }

    /// <summary>400 (code <see cref="IncompleteCode"/>) when a required race or background choice is not answered.</summary>
    public async Task EnsureCompleteAsync(Character character, CancellationToken cancellationToken = default)
    {
        var missing = Missing(character, await PlanAsync(character, cancellationToken));
        if (missing.Count > 0)
        {
            throw AppException.Validation(
                "originChoices",
                $"Faltan elecciones de raza o trasfondo: {string.Join(", ", missing.Select(m => $"«{m}»"))}.",
                IncompleteCode);
        }
    }

    public static OriginChoicesDto ToDto(Character character, IReadOnlyList<PlannedOriginChoice> plan) => new(
        character.Id,
        Missing(character, plan).Count == 0,
        plan.Select(c =>
            {
                var answer = character.OriginChoice(c.Key)?.Selection;
                return new OriginChoiceDto(
                    c.Key,
                    c.Name,
                    c.Kind,
                    c.Source,
                    c.Choose,
                    c.Required,
                    c.Amount,
                    c.FreeText,
                    c.Note,
                    c.Options.Select(o => new LevelUpOptionDto(
                            o.Index,
                            o.Name,
                            o.Description,
                            o.PrerequisitesText,
                            o.Eligible,
                            o.Reason,
                            o.SpellLevel,
                            o.Preview,
                            o.Definition?.AbilityIncrease is { } increase ? new AbilityIncreaseDto(increase.Amount, increase.From) : null,
                            o.SpellCategory)
                        {
                            DamageType = o.DamageType,
                        })
                        .ToList(),
                    (answer?.Selected ?? []).Select(ChoiceItemDto.From).ToList(),
                    answer?.Feat is { } feat ? ChoiceItemDto.From(feat) : null,
                    answer?.Ability);
            })
            .ToList());

    private static int Answered(CharacterChoice? choice) =>
        choice?.Selection is not { } selection ? 0 : selection.Feat is not null ? 1 : selection.Selected.Count;

    private static IEnumerable<PlannedOriginChoice> Plan(
        Character character,
        string prefix,
        string source,
        RaceChoices choices,
        IReadOnlyList<SkillDefinition> skills,
        IReadOnlyList<SpellDefinition> spells,
        IReadOnlyList<OptionDefinition> feats,
        CharacterSheet? sheet)
    {
        var origin = source == BreakdownSources.Background ? "trasfondo" : source == BreakdownSources.Subrace ? "subraza" : "raza";

        // A proficiency the character already has from elsewhere cannot be picked again (this choice's own picks can).
        PlannedOption Proficiency(string key, ProficiencyType type, string index, string name, IReadOnlyList<string> description)
        {
            var own = character.OriginChoice(key)?.Selection.Selected.Any(s => s.Index == index) ?? false;
            var taken = !own && character.FindProficiency(type, index) is not null;
            return new PlannedOption(index, name, description, null, !taken, taken ? "Ya tienes esta competencia." : null, null, null, []);
        }

        PlannedOriginChoice Picks(string key, string name, string kind, PickChoice pick, IReadOnlyList<PlannedOption> options, bool freeText, int required, string note) =>
            new(key, name, kind, source, pick.Choose, required, null, freeText, note, options);

        if (choices.AbilityBonuses is { } bonuses)
        {
            yield return new PlannedOriginChoice(
                prefix + OriginChoiceKeys.AbilityBonuses,
                $"Mejora de característica ({origin})",
                OriginChoiceKeys.AbilityBonusKind,
                source,
                bonuses.Choose,
                bonuses.Choose,
                bonuses.Amount,
                false,
                $"+{bonuses.Amount} a {bonuses.Choose} características distintas.",
                bonuses.From.Select(o => new PlannedOption(o.Index, BreakdownLabels.Ability(o.Index), [], null, true, null, null, null, [])).ToList());
        }

        if (choices.Skills is { } skillChoice)
        {
            var key = prefix + OriginChoiceKeys.Skills;
            var allowed = skillChoice.From.Count == 0
                ? skills.Select(s => (s.Index, s.Name, s.Description)).ToList()
                : skillChoice.From.Select(o => (o.Index, Name: skills.FirstOrDefault(s => s.Index == o.Index)?.Name ?? o.Name, Description: skills.FirstOrDefault(s => s.Index == o.Index)?.Description ?? [])).ToList();
            var options = allowed.Select(s => Proficiency(key, ProficiencyType.Skill, s.Index, s.Name, s.Description)).ToList();
            yield return Picks(key, $"Habilidades ({origin})", OriginChoiceKeys.SkillKind, skillChoice, options, false, Math.Min(skillChoice.Choose, options.Count(o => o.Eligible)), string.Empty);
        }

        if (choices.Tools is { } toolChoice)
        {
            var key = prefix + OriginChoiceKeys.Tools;
            var options = toolChoice.From.Select(o => Proficiency(key, ProficiencyType.Tool, o.Index, o.Name, [])).ToList();
            var required = options.Count == 0 ? toolChoice.Choose : Math.Min(toolChoice.Choose, options.Count(o => o.Eligible));
            yield return Picks(key, $"Herramientas ({origin})", OriginChoiceKeys.ToolKind, toolChoice, options, options.Count == 0, required, string.Empty);
        }

        if (choices.Languages is { } languageChoice)
        {
            var key = prefix + OriginChoiceKeys.Languages;
            var names = languageChoice.From.Count == 0 ? SrdLanguages.All : languageChoice.From.Select(o => o.Index).ToList();
            var options = names.Select(n => Proficiency(key, ProficiencyType.Language, n, n, [])).ToList();
            yield return Picks(
                key,
                $"Idiomas ({origin})",
                OriginChoiceKeys.LanguageKind,
                languageChoice,
                options,
                languageChoice.From.Count == 0,
                0,
                "Opcional: el asistente de creación ya pide los idiomas en su propio paso.");
        }

        if (choices.Cantrip is { } cantrip)
        {
            var allowed = cantrip.From.Select(o => o.Index).ToHashSet(StringComparer.Ordinal);
            var options = spells
                .Where(s => s.Level == 0)
                .Where(s => allowed.Count > 0 ? allowed.Contains(s.Index) : cantrip.SpellList == ChoiceFilter.AnyList || s.ClassIndexes.Contains(cantrip.SpellList, StringComparer.Ordinal))
                .OrderBy(s => s.Name, StringComparer.Ordinal)
                .Select(s => new PlannedOption(s.Index, s.Name, s.Description.Take(1).ToList(), null, true, null, 0, null, [], s.Category.ToString()))
                .ToList();
            yield return new PlannedOriginChoice(
                prefix + OriginChoiceKeys.Cantrip,
                $"Truco ({origin})",
                OriginChoiceKeys.CantripKind,
                source,
                cantrip.Choose,
                Math.Min(cantrip.Choose, options.Count),
                null,
                false,
                cantrip.SpellList == ChoiceFilter.AnyList ? "Siempre preparado." : $"De la lista de {BreakdownLabels.Class(cantrip.SpellList)}; siempre preparado.",
                options);
        }

        if (choices.Feats is { } featChoice)
        {
            var options = feats
                .OrderBy(f => f.Name, StringComparer.Ordinal)
                .Select(f =>
                {
                    var reasons = f.Prerequisites.Abilities
                        .Where(a => Abilities.IsValid(a.Key) && sheet is not null && sheet.Abilities[a.Key].Score < a.Value)
                        .Select(a => $"Requiere {BreakdownLabels.Ability(a.Key)} {a.Value}.")
                        .ToList();
                    var reason = reasons.Count == 0 ? null : string.Join(" ", reasons);
                    return new PlannedOption(f.Index, f.Name, f.Description, f.PrerequisitesText, reason is null, reason, null, f, []);
                })
                .ToList();
            yield return new PlannedOriginChoice(
                prefix + OriginChoiceKeys.Feat,
                $"Dote ({origin})",
                OriginChoiceKeys.FeatKind,
                source,
                featChoice.Choose,
                Math.Min(featChoice.Choose, options.Count(o => o.Eligible)),
                null,
                false,
                string.Empty,
                options);
        }

        foreach (var trait in choices.TraitOptions)
        {
            var options = trait.Options
                .Select(o => new PlannedOption(o.Index, o.Name, o.Description, null, true, null, null, null, []) { DamageType = o.DamageType })
                .ToList();
            yield return new PlannedOriginChoice(
                prefix + OriginChoiceKeys.TraitPrefix + trait.Key,
                trait.Name,
                OriginChoiceKeys.TraitOptionKind,
                source,
                trait.Choose,
                Math.Min(trait.Choose, options.Count),
                null,
                false,
                string.Empty,
                options);
        }
    }
}

/// <summary>
/// The origin choices of a character: the owner of a draft and DMs read and answer them directly (no approval); the owner
/// of an active character cannot change them (409).
/// </summary>
public sealed class OriginChoicesHandler(
    CharacterLoader loader,
    OriginChoicesPlanner planner,
    ICharacterSheetService sheets,
    IUnitOfWork unitOfWork,
    ICampaignNotifier notifier,
    IDateTimeProvider clock)
{
    public async Task<OriginChoicesDto> GetAsync(Guid currentUserId, Guid characterId, CancellationToken cancellationToken = default)
    {
        var loaded = await loader.LoadAsync(characterId, currentUserId, cancellationToken);
        if (!loaded.Character.CanViewSheet(currentUserId, loaded.IsDm))
        {
            throw AppException.Forbidden("Solo el dueño del personaje o un DM pueden ver sus elecciones.");
        }

        return OriginChoicesPlanner.ToDto(loaded.Character, await planner.PlanAsync(loaded.Character, cancellationToken));
    }

    public async Task<OriginChoicesDto> SaveAsync(Guid currentUserId, Guid characterId, SaveOriginChoicesRequest request, CancellationToken cancellationToken = default)
    {
        var loaded = await loader.LoadAsync(characterId, currentUserId, cancellationToken);
        var character = loaded.Character;
        if (!character.CanViewSheet(currentUserId, loaded.IsDm))
        {
            throw AppException.Forbidden("Solo el dueño del personaje o un DM pueden cambiar sus elecciones.");
        }

        if (!loaded.IsDm && character.Status != CharacterStatus.Draft)
        {
            throw AppException.Conflict("Las elecciones de raza y trasfondo de un personaje activo solo las cambia un DM.");
        }

        var plan = await planner.PlanAsync(character, cancellationToken);
        var resolved = new List<(PlannedOriginChoice Choice, ChoiceSelection? Selection)>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var answer in request.Choices ?? [])
        {
            var key = answer.Key!.Trim();
            if (!seen.Add(key))
            {
                throw AppException.Validation("choices", $"La elección '{key}' está repetida.");
            }

            var choice = plan.FirstOrDefault(c => c.Key == key)
                ?? throw AppException.Validation("choices", $"La elección '{key}' no corresponde a la raza ni al trasfondo del personaje.");
            resolved.Add((choice, Resolve(choice, answer)));
        }

        var now = clock.UtcNow;
        foreach (var (choice, selection) in resolved)
        {
            if (selection is null)
            {
                character.RemoveOriginChoices(k => k == choice.Key);
            }
            else
            {
                character.RecordOriginChoice(choice.Key, selection, now);
            }
        }

        await sheets.RecalculateAsync(character, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await notifier.CharacterUpdatedAsync(character.CampaignId, character.Id, now, cancellationToken);
        return OriginChoicesPlanner.ToDto(character, await planner.PlanAsync(character, cancellationToken));
    }

    /// <summary>Validates an answer against its planned choice; null removes the answer.</summary>
    private static ChoiceSelection? Resolve(PlannedOriginChoice choice, LevelUpChoiceAnswer answer)
    {
        var selected = answer.Selected;
        if (selected.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null
            || (selected.ValueKind == JsonValueKind.Array && selected.GetArrayLength() == 0))
        {
            return null;
        }

        if (choice.Kind == OriginChoiceKeys.FeatKind)
        {
            return ResolveFeat(choice, selected);
        }

        if (selected.ValueKind != JsonValueKind.Array)
        {
            throw AppException.Validation("choices", $"La elección «{choice.Name}» espera una lista en selected.");
        }

        var picks = new List<string>();
        foreach (var element in selected.EnumerateArray())
        {
            if (element.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(element.GetString()))
            {
                throw AppException.Validation("choices", $"La elección «{choice.Name}» solo admite textos en selected.");
            }

            picks.Add(element.GetString()!.Trim());
        }

        if (picks.Distinct(StringComparer.OrdinalIgnoreCase).Count() != picks.Count)
        {
            throw AppException.Validation("choices", $"La elección «{choice.Name}» repite una opción.");
        }

        if (picks.Count != choice.Choose)
        {
            throw AppException.Validation("choices", $"La elección «{choice.Name}» necesita exactamente {choice.Choose} {(choice.Choose == 1 ? "opción" : "opciones")}.");
        }

        var items = new List<ChoiceItem>();
        foreach (var pick in picks)
        {
            var option = choice.Option(pick) ?? choice.Options.FirstOrDefault(o => string.Equals(o.Index, pick, StringComparison.OrdinalIgnoreCase));
            if (option is null)
            {
                if (!choice.FreeText)
                {
                    throw AppException.Validation("choices", $"«{pick}» no es una opción de «{choice.Name}».");
                }

                if (pick.Length > Character.IndexMaxLength)
                {
                    throw AppException.Validation("choices", $"«{choice.Name}»: cada valor admite como máximo {Character.IndexMaxLength} caracteres.");
                }

                items.Add(new ChoiceItem(pick, pick));
                continue;
            }

            if (!option.Eligible)
            {
                throw AppException.Validation("choices", $"«{option.Name}» no se puede elegir: {option.Reason}");
            }

            items.Add(new ChoiceItem(option.Index, option.Name));
        }

        return new ChoiceSelection
        {
            Kind = choice.Kind,
            Name = choice.Name,
            Selected = items,
            Asi = choice.Kind == OriginChoiceKeys.AbilityBonusKind ? items.ToDictionary(i => i.Index, _ => choice.Amount ?? 1, StringComparer.Ordinal) : null,
        };
    }

    /// <summary><c>{ "feat": "grappler", "ability": "str" }</c> or <c>["grappler"]</c> when the feat raises no ability or only one.</summary>
    private static ChoiceSelection ResolveFeat(PlannedOriginChoice choice, JsonElement selected)
    {
        string? featIndex = null;
        string? ability = null;
        if (selected.ValueKind == JsonValueKind.Array && selected.GetArrayLength() == 1 && selected[0].ValueKind == JsonValueKind.String)
        {
            featIndex = selected[0].GetString()?.Trim();
        }
        else if (selected.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in selected.EnumerateObject())
            {
                switch (property.Name.ToLowerInvariant())
                {
                    case "feat" when property.Value.ValueKind == JsonValueKind.String:
                        featIndex = property.Value.GetString()?.Trim();
                        break;
                    case "ability" when property.Value.ValueKind is JsonValueKind.String or JsonValueKind.Null:
                        ability = property.Value.GetString()?.Trim().ToLowerInvariant();
                        break;
                    default:
                        throw AppException.Validation("choices", $"La elección «{choice.Name}» tiene un campo desconocido: {property.Name}.");
                }
            }
        }

        if (string.IsNullOrEmpty(featIndex))
        {
            throw AppException.Validation("choices", $"«{choice.Name}»: envía {{ \"feat\": \"...\", \"ability\": \"...\" }}.");
        }

        var feat = choice.Option(featIndex) ?? throw AppException.Validation("choices", $"«{featIndex}» no es una dote disponible.");
        if (!feat.Eligible)
        {
            throw AppException.Validation("choices", $"«{feat.Name}» no cumple los requisitos: {feat.Reason}");
        }

        string? raised = null;
        if (feat.Definition?.AbilityIncrease is { } increase)
        {
            raised = string.IsNullOrEmpty(ability) ? (increase.From.Count == 1 ? increase.From[0] : null) : ability;
            if (raised is null || !Abilities.IsValid(raised) || (increase.From.Count > 0 && !increase.From.Contains(raised, StringComparer.Ordinal)))
            {
                throw AppException.Validation("choices", $"«{feat.Name}»: elige la característica que sube ({(increase.From.Count == 0 ? "cualquiera" : string.Join(", ", increase.From))}).");
            }
        }
        else if (!string.IsNullOrEmpty(ability))
        {
            throw AppException.Validation("choices", $"«{feat.Name}» no sube ninguna característica.");
        }

        return new ChoiceSelection
        {
            Kind = choice.Kind,
            Name = choice.Name,
            SetId = OptionSets.Feats,
            Feat = new ChoiceItem(feat.Index, feat.Name),
            Ability = raised,
        };
    }
}
