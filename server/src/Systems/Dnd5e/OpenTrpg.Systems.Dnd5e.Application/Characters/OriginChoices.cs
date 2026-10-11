using System.Text.Json;
using OpenTrpg.Core.Application.Abstractions;
using OpenTrpg.Core.Application.Abstractions.Persistence;
using OpenTrpg.Core.Application.Common;
using OpenTrpg.Core.Domain.Catalog;
using OpenTrpg.Core.Domain.Characters;
using FluentValidation;
using OpenTrpg.Core.Application;
using OpenTrpg.Core.Application.Characters;
using OpenTrpg.Systems.Dnd5e.Application;
using OpenTrpg.Systems.Dnd5e.Application.Abstractions;
using OpenTrpg.Systems.Dnd5e.Application.Abstractions.Persistence;
using OpenTrpg.Systems.Dnd5e.Application.Campaigns;
using OpenTrpg.Systems.Dnd5e.Application.Characters;
using OpenTrpg.Systems.Dnd5e.Domain.Catalog;
using OpenTrpg.Systems.Dnd5e.Domain.Characters;

namespace OpenTrpg.Systems.Dnd5e.Application.Characters;

// Phase 19: decisions of the race, subrace and background (GET/PUT /characters/{id}/origin-choices). The level-1
// choices of the first class (fighting style, expertise...) travel in the same flow (source "class").

/// <summary>
/// One decision of the race, subrace or background of a character, or a level-1 choice of its first class. <see cref="Required"/> picks are needed before the
/// draft can be activated (0 for optional ones: languages, which the creation wizard already asks for in its own step).
/// <see cref="Selected"/> is the current answer (empty when not answered); feats also carry <see cref="Feat"/> and the
/// <see cref="Ability"/> they raise.
/// </summary>
/// <param name="Key">"race.abilityBonuses", "race.skills", "race.languages", "race.tools", "race.cantrip", "race.feat",
/// "race.trait.&lt;key&gt;"; subrace ones start with "race.subrace."; background ones with "background."; the level-1
/// choices of the first class are "class.&lt;rule key&gt;".</param>
/// <param name="Kind">AbilityBonus, Skill, Language, Tool, Cantrip, Feat or TraitOption; for the class choices, the
/// <c>LevelChoiceKind</c> of the rule (OptionSet, Expertise, Skill, Tool, Language, Weapon, SpellbookSpells...).</param>
/// <param name="Source">"race", "subrace", "background", "campaign" (starter feat) or "class".</param>
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
    string? Ability)
{
    /// <summary>Feats with <c>grants.removeSavingThrows</c> to choose: the saving throw proficiencies the player gives up.</summary>
    public IReadOnlyList<string> RemovedSavingThrows { get; init; } = [];

    /// <summary>Class of a level choice of the first class (source "class"); null for the origin ones.</summary>
    public string? ClassIndex { get; init; }

    /// <summary>Class level of a class choice (1); null for the origin ones.</summary>
    public int? Level { get; init; }

    /// <summary>Option set of a class choice of options, or null.</summary>
    public string? SetId { get; init; }

    /// <summary>
    /// Choice of an option (<c>options[].choices</c>) of another class choice: only asked, and only required, when
    /// <c>index</c> is picked in the choice <c>choiceKey</c> (a "class." key). Null otherwise.
    /// </summary>
    public OptionRequirementDto? Requires { get; init; }

    /// <summary>Why fewer picks than the rule gives are required (no eligible options), or null.</summary>
    public string? Warning { get; init; }
}

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

    /// <summary>The level choice of the first class behind a "class." choice, or null for the origin ones.</summary>
    public PlannedChoice? Class { get; init; }

    /// <summary>Class of <see cref="Class"/>.</summary>
    public string? ClassIndex { get; init; }

    /// <summary>The "class." choice and option that <see cref="Class"/> requires (options[].choices), or null.</summary>
    public OptionRequirementDto? Requires { get; init; }
}

/// <summary>
/// Builds the origin choices of a character from its race, subrace and background (<see cref="RaceChoices"/>), plus the
/// level-1 choices of its first class (planned by <see cref="LevelUpPlanner.BuildInitialClassAsync"/>), and checks that
/// the required ones are answered.
/// </summary>
public sealed class OriginChoicesPlanner(ICatalogRepository catalog, ICharacterSheetService sheets, ICampaignRulesRepository rules, LevelUpPlanner levelUp)
{
    public const string IncompleteCode = "origin-choices-incomplete";

    /// <summary>Prefix of the keys of the level-1 choices of the first class ("class.fighting-style").</summary>
    public const string ClassPrefix = "class.";

    /// <summary>Source of the level-1 choices of the first class.</summary>
    public const string ClassSource = "class";

    /// <summary>Level at which the choices of the first class asked here are recorded.</summary>
    public const int ClassLevel = 1;

    /// <summary>Source of the optional starter feat (not the race nor the background).</summary>
    public const string StarterSource = "campaign";

    /// <summary>The starter feat can be taken now: the campaign enables starter feats and the character is level 1.</summary>
    public async Task<bool> StarterFeatAllowedAsync(Dnd5eCharacter character, CancellationToken cancellationToken = default) =>
        character.TotalLevel <= 1 && await CampaignRulesHandler.StarterFeatsEnabledAsync(rules, character.CampaignId, cancellationToken);

    public async Task<IReadOnlyList<PlannedOriginChoice>> PlanAsync(Dnd5eCharacter character, CancellationToken cancellationToken = default)
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

        var result = await PlanOriginAsync(character, race, sources, cancellationToken);
        if (await levelUp.BuildInitialClassAsync(character, new HashSet<string>(StringComparer.Ordinal), cancellationToken) is { } initial)
        {
            result.AddRange(initial.Choices.Select(c => ClassChoice(initial.Class.Index, c)));
        }

        return result;
    }

    /// <summary>A level-1 choice of the first class as an origin choice ("class." key, source "class").</summary>
    private static PlannedOriginChoice ClassChoice(string classIndex, PlannedChoice choice) =>
        new(
            ClassPrefix + choice.Rule.Key,
            choice.Rule.Name,
            choice.Rule.Kind.ToString(),
            ClassSource,
            choice.Rule.Choose,
            choice.Required,
            null,
            choice.FreeText,
            choice.Note,
            choice.Options)
        {
            Class = choice,
            ClassIndex = classIndex,
            Requires = choice.Requires is { } requires ? requires with { ChoiceKey = ClassPrefix + requires.ChoiceKey } : null,
        };

    private async Task<List<PlannedOriginChoice>> PlanOriginAsync(
        Dnd5eCharacter character,
        RaceDefinition? race,
        List<(string Prefix, string Source, RaceChoices Choices)> sources,
        CancellationToken cancellationToken)
    {
        // The starter feat (H15): offered at level 1 when the campaign enables it; an answer already given stays visible.
        var starterAllowed = await StarterFeatAllowedAsync(character, cancellationToken);
        var starterAnswered = character.OriginChoice(OriginChoiceKeys.StarterFeat) is not null;
        if (sources.All(s => s.Choices.IsEmpty) && !starterAllowed && !starterAnswered)
        {
            return [];
        }

        var skills = await catalog.ListSkillsAsync(cancellationToken);
        var spells = sources.Any(s => s.Choices.Cantrip is not null) ? await catalog.ListAllSpellsAsync(cancellationToken) : [];
        var featSets = sources.Where(s => s.Choices.Feats is not null).Select(s => s.Choices.Feats!.SetId ?? OptionSets.Feats).ToList();
        if (starterAllowed || starterAnswered)
        {
            featSets.Add(OptionSets.Feats);
        }

        var feats = featSets.Count > 0 ? await catalog.ListOptionsBySetAsync(featSets.Distinct(StringComparer.Ordinal).ToList(), cancellationToken) : [];
        var sheet = feats.Count > 0 ? await sheets.CalculateAsync(character, cancellationToken) : null;
        var toolIndexes = sources.SelectMany(s => s.Choices.Tools?.From ?? []).Select(o => o.Index).Distinct(StringComparer.Ordinal).ToList();
        var tools = toolIndexes.Count > 0 ? await catalog.ListCatalogItemsByIndexAsync(toolIndexes, cancellationToken) : [];

        var result = new List<PlannedOriginChoice>();
        foreach (var (prefix, source, choices) in sources)
        {
            result.AddRange(Plan(character, prefix, source, choices, skills, spells, feats, tools, sheet, race?.Size));
        }

        var starters = feats.Where(f => f.Starter && f.SetId == OptionSets.Feats).ToList();
        if ((starterAllowed && starters.Count > 0) || starterAnswered)
        {
            var options = FeatOptions(character, OriginChoiceKeys.StarterFeat, starters, sheet, race?.Size);
            result.Add(new PlannedOriginChoice(
                OriginChoiceKeys.StarterFeat,
                "Dote inicial",
                OriginChoiceKeys.FeatKind,
                StarterSource,
                1,
                0,
                null,
                false,
                starterAllowed
                    ? "Opcional: una dote inicial que la campaña permite a nivel 1, además de todo lo demás."
                    : "Solo se elige a nivel 1 con las dotes iniciales activadas en la campaña.",
                options));
        }

        return result;
    }

    /// <summary>The required choices without a complete answer (names), empty when everything is answered.</summary>
    public static IReadOnlyList<string> Missing(Dnd5eCharacter character, IReadOnlyList<PlannedOriginChoice> plan)
    {
        ArgumentNullException.ThrowIfNull(character);
        ArgumentNullException.ThrowIfNull(plan);
        return plan
            .Where(c => c.Required > 0 && RequirementMet(character, c) && Answered(Stored(character, c)) < c.Required)
            .Select(c => c.Name)
            .ToList();
    }

    /// <summary>The recorded answer of a choice: the origin choice of its key, or the level-1 choice of the first class.</summary>
    public static CharacterChoice? Stored(Dnd5eCharacter character, PlannedOriginChoice choice) =>
        choice.Class is { } planned
            ? LevelUpPlanner.InitialClassChoice(character, choice.ClassIndex!, planned.Rule.Key)
            : character.OriginChoice(choice.Key);

    /// <summary>A class choice of an option applies only while that option is recorded in its parent choice.</summary>
    public static bool RequirementMet(Dnd5eCharacter character, PlannedOriginChoice choice)
    {
        if (choice.Class?.Requires is not { } requires)
        {
            return true;
        }

        var parent = LevelUpPlanner.InitialClassChoice(character, choice.ClassIndex!, requires.ChoiceKey)?.Selection;
        return parent is not null && LevelUpPlanner.PickedIndexes(parent).Contains(requires.Index, StringComparer.Ordinal);
    }

    /// <summary>400 (code <see cref="IncompleteCode"/>) when a required race, background or first-class choice is not answered.</summary>
    public async Task EnsureCompleteAsync(Dnd5eCharacter character, CancellationToken cancellationToken = default)
    {
        var missing = Missing(character, await PlanAsync(character, cancellationToken));
        if (missing.Count > 0)
        {
            throw AppException.Validation(
                "originChoices",
                $"Faltan elecciones de raza, trasfondo o clase: {string.Join(", ", missing.Select(m => $"«{m}»"))}.",
                IncompleteCode);
        }
    }

    public static OriginChoicesDto ToDto(Dnd5eCharacter character, IReadOnlyList<PlannedOriginChoice> plan) => new(
        character.Id,
        Missing(character, plan).Count == 0,
        plan.Select(c =>
            {
                var answer = Stored(character, c)?.Selection;
                if (c.Class is { } planned)
                {
                    var dto = planned.ToDto();
                    return new OriginChoiceDto(
                        c.Key,
                        c.Name,
                        c.Kind,
                        c.Source,
                        c.Choose,
                        c.Required,
                        null,
                        c.FreeText,
                        c.Note,
                        dto.Options.Select(o => o.Requires is { } needs ? o with { Requires = needs with { ChoiceKey = ClassPrefix + needs.ChoiceKey } } : o).ToList(),
                        (answer?.Selected ?? []).Select(ChoiceItemDto.From).ToList(),
                        answer?.Feat is { } classFeat ? ChoiceItemDto.From(classFeat) : null,
                        answer?.Ability)
                    {
                        RemovedSavingThrows = answer?.RemovedSavingThrows ?? [],
                        ClassIndex = c.ClassIndex,
                        Level = ClassLevel,
                        SetId = planned.Rule.SetId,
                        Requires = c.Requires,
                        Warning = planned.Warning,
                    };
                }

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
                            ItemId = o.ItemId,
                            RemoveSavingThrows = o.Definition?.Grants.RemoveSavingThrows is { } remove
                                ? new RemoveSavingThrowsDto(remove.Choose, remove.From)
                                : null,
                            SkillChoicesDelta = o.Definition?.Grants.SkillChoicesDelta ?? 0,
                        })
                        .ToList(),
                    (answer?.Selected ?? []).Select(ChoiceItemDto.From).ToList(),
                    answer?.Feat is { } feat ? ChoiceItemDto.From(feat) : null,
                    answer?.Ability)
                {
                    RemovedSavingThrows = answer?.RemovedSavingThrows ?? [],
                };
            })
            .ToList());

    /// <summary>
    /// The feats of an origin choice: at creation only what the origin decides counts (abilities, race and size), and a
    /// feat another origin choice already took cannot be taken again.
    /// </summary>
    private static List<PlannedOption> FeatOptions(Dnd5eCharacter character, string key, IReadOnlyList<OptionDefinition> feats, CharacterSheet? sheet, string? size)
    {
        var taken = character.OriginChoices
            .Where(c => c.Key != key && c.Selection.Feat is not null)
            .Select(c => c.Selection.Feat!.Index)
            .ToHashSet(StringComparer.Ordinal);
        return feats
            .OrderBy(f => f.Name, StringComparer.Ordinal)
            .Select(f =>
            {
                var originPrerequisites = new OptionPrerequisites(null, null, null, f.Prerequisites.Abilities) { Races = f.Prerequisites.Races, Sizes = f.Prerequisites.Sizes };
                var reason = taken.Contains(f.Index) ? "Ya tienes esta dote." : sheet is null ? null : PrerequisiteEvaluator.WhyNot(originPrerequisites, new PrerequisiteFacts
                {
                    Character = character,
                    Sheet = sheet,
                    CharacterLevel = character.TotalLevel,
                    ClassLevel = character.TotalLevel,
                    Size = size,
                });
                return new PlannedOption(f.Index, f.Name, f.Description, f.PrerequisitesText, reason is null, reason, null, f, []);
            })
            .ToList();
    }

    private static int Answered(CharacterChoice? choice) =>
        choice?.Selection is not { } selection ? 0
        : selection.Feat is not null ? 1
        : selection.Selected.Count > 0 ? selection.Selected.Count
        : selection.Asi is { Count: > 0 } ? 1
        : 0;

    private static IEnumerable<PlannedOriginChoice> Plan(
        Dnd5eCharacter character,
        string prefix,
        string source,
        RaceChoices choices,
        IReadOnlyList<SkillDefinition> skills,
        IReadOnlyList<SpellDefinition> spells,
        IReadOnlyList<OptionDefinition> feats,
        IReadOnlyList<ItemTemplate> tools,
        CharacterSheet? sheet,
        string? size = null)
    {
        // Every option carries a description (phase 29): the catalog text or, when it has none, one built here
        // (OriginOptionDescriptions).
        var origin = source == BreakdownSources.Background ? "trasfondo" : source == BreakdownSources.Subrace ? "subraza" : "raza";

        // A proficiency the character already has from elsewhere cannot be picked again (this choice's own picks can).
        PlannedOption Proficiency(string key, ProficiencyType type, string index, string name, IReadOnlyList<string> description, ItemTemplate? item = null)
        {
            var own = character.OriginChoice(key)?.Selection.Selected.Any(s => s.Index == index) ?? false;
            var taken = !own && character.FindProficiency(type, index) is not null;
            return new PlannedOption(index, name, description, null, !taken, taken ? "Ya tienes esta competencia." : null, null, null, [])
            {
                ItemId = item?.Id.ToString(),
            };
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
                bonuses.From.Select(o => new PlannedOption(o.Index, BreakdownLabels.Ability(o.Index), OriginOptionDescriptions.Ability(o.Index), null, true, null, null, null, [])).ToList());
        }

        if (choices.Skills is { } skillChoice)
        {
            var key = prefix + OriginChoiceKeys.Skills;
            var allowed = skillChoice.From.Count == 0
                ? skills.Select(s => (s.Index, s.Name, Description: OriginOptionDescriptions.Skill(s, s.Name))).ToList()
                : skillChoice.From
                    .Select(o => (Option: o, Skill: skills.FirstOrDefault(s => s.Index == o.Index)))
                    .Select(p => (p.Option.Index, Name: p.Skill?.Name ?? p.Option.Name, Description: OriginOptionDescriptions.Skill(p.Skill, p.Skill?.Name ?? p.Option.Name)))
                    .ToList();
            var options = allowed.Select(s => Proficiency(key, ProficiencyType.Skill, s.Index, s.Name, s.Description)).ToList();
            yield return Picks(key, $"Habilidades ({origin})", OriginChoiceKeys.SkillKind, skillChoice, options, false, Math.Min(skillChoice.Choose, options.Count(o => o.Eligible)), string.Empty);
        }

        if (choices.Tools is { } toolChoice)
        {
            var key = prefix + OriginChoiceKeys.Tools;
            // Every option is a catalog item index: the item gives the name, the description and the itemId.
            var options = toolChoice.From
                .Select(o => (Option: o, Item: tools.FirstOrDefault(t => string.Equals(t.Index, o.Index, StringComparison.Ordinal))))
                .Select(p => Proficiency(key, ProficiencyType.Tool, p.Option.Index, p.Item?.Name ?? p.Option.Name, OriginOptionDescriptions.Tool(p.Item), p.Item))
                .ToList();
            var required = options.Count == 0 ? toolChoice.Choose : Math.Min(toolChoice.Choose, options.Count(o => o.Eligible));
            yield return Picks(key, $"Herramientas ({origin})", OriginChoiceKeys.ToolKind, toolChoice, options, options.Count == 0, required, OriginOptionDescriptions.ToolChoiceNote(toolChoice.Choose));
        }

        if (choices.Languages is { } languageChoice)
        {
            var key = prefix + OriginChoiceKeys.Languages;
            var names = languageChoice.From.Count == 0 ? SrdLanguages.All : languageChoice.From.Select(o => o.Index).ToList();
            var options = names.Select(n => Proficiency(key, ProficiencyType.Language, n, n, OriginOptionDescriptions.Language(n))).ToList();
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
                .Select(s => new PlannedOption(s.Index, s.Name, s.Description.Count > 0 ? s.Description.Take(1).ToList() : ["Truco."], null, true, null, 0, null, [], s.Category.ToString()))
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
            // Feats of the set of the choice (the feats set by default); starter feats only come with "Dote inicial".
            var setId = featChoice.SetId ?? OptionSets.Feats;
            var options = FeatOptions(character, prefix + OriginChoiceKeys.Feat, feats.Where(f => f.SetId == setId && !f.Starter).ToList(), sheet, size);
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
    Dnd5eCharacterLoader loader,
    OriginChoicesPlanner planner,
    LevelUpPlanner levelUp,
    ICatalogRepository catalog,
    ICharacterSheetService sheets,
    IUnitOfWork unitOfWork,
    ICampaignNotifier notifier,
    IDateTimeProvider clock)
{
    public const string StarterFeatNotAllowedCode = "starter-feat-not-allowed";

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

        var now = clock.UtcNow;
        await ApplyAsync(character, request, now, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await notifier.CharacterUpdatedAsync(character.CampaignId, character.Id, now, cancellationToken);
        return OriginChoicesPlanner.ToDto(character, await planner.PlanAsync(character, cancellationToken));
    }

    /// <summary>
    /// Records (or removes) the answers and recalculates the sheet; the caller saves. The race and background answers go
    /// first, so the level-1 choices of the first class ("class." keys) are checked against the origin just saved.
    /// </summary>
    public async Task ApplyAsync(Dnd5eCharacter character, SaveOriginChoicesRequest request, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var plan = await planner.PlanAsync(character, cancellationToken);
        var resolved = new List<(PlannedOriginChoice Choice, ChoiceSelection? Selection)>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var classAnswers = new List<LevelUpChoiceAnswer>();
        foreach (var answer in request.Choices ?? [])
        {
            var key = answer.Key!.Trim();
            if (!seen.Add(key))
            {
                throw AppException.Validation("choices", $"La elección '{key}' está repetida.");
            }

            if (key.StartsWith(OriginChoicesPlanner.ClassPrefix, StringComparison.Ordinal))
            {
                classAnswers.Add(answer with { Key = key[OriginChoicesPlanner.ClassPrefix.Length..] });
                continue;
            }

            var choice = plan.FirstOrDefault(c => c.Key == key)
                ?? throw AppException.Validation(
                    "choices",
                    key == OriginChoiceKeys.StarterFeat
                        ? "La dote inicial solo se elige a nivel 1 y si la campaña tiene activadas las dotes iniciales."
                        : $"La elección '{key}' no corresponde a la raza ni al trasfondo del personaje.",
                    key == OriginChoiceKeys.StarterFeat ? StarterFeatNotAllowedCode : null);
            var selection = Resolve(choice, answer);
            if (selection is not null && key == OriginChoiceKeys.StarterFeat && !await planner.StarterFeatAllowedAsync(character, cancellationToken))
            {
                throw AppException.Validation("choices", "La dote inicial solo se elige a nivel 1 y si la campaña tiene activadas las dotes iniciales.", StarterFeatNotAllowedCode);
            }

            resolved.Add((choice, selection));
        }

        // Feats that stop being picked lose their granted spells (ChoiceGrants); the new ones grant theirs.
        var replacedFeats = resolved
            .Select(r => character.OriginChoice(r.Choice.Key)?.Selection.Feat?.Index)
            .OfType<string>()
            .ToList();
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

        if (replacedFeats.Count > 0 || resolved.Any(r => r.Selection?.Feat is not null))
        {
            await ChoiceGrants.ApplyAsync(catalog, character, replacedFeats, now, cancellationToken);
        }

        if (classAnswers.Count > 0)
        {
            await ApplyClassAsync(character, classAnswers, now, cancellationToken);
        }

        await sheets.RecalculateAsync(character, cancellationToken);
    }

    /// <summary>
    /// The answers to the level-1 choices of the first class (keys without "class."), checked as the level-up checks them
    /// and recorded as choices of class level 1: an answer replaces the one recorded (undoing its effects), an empty one
    /// removes it, and the choices of an option that is no longer picked are dropped.
    /// </summary>
    private async Task ApplyClassAsync(Dnd5eCharacter character, List<LevelUpChoiceAnswer> classAnswers, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var answers = LevelChoiceAnswers.Parse(classAnswers);
        var plan = await levelUp.BuildInitialClassAsync(character, LevelChoiceAnswers.PendingPicks(answers), cancellationToken, LevelChoiceAnswers.PendingChoices(answers))
            ?? throw AppException.Validation("choices", $"La elección '{OriginChoicesPlanner.ClassPrefix}{answers.Keys.First()}' no corresponde a la clase del personaje.");
        var classIndex = plan.Class.Index;
        var level = OriginChoicesPlanner.ClassLevel;

        bool Picked(OptionRequirementDto requires) =>
            answers.TryGetValue(requires.ChoiceKey, out var parent)
                ? parent.Selected.Contains(requires.Index, StringComparer.Ordinal) || parent.Feat == requires.Index
                : LevelUpPlanner.InitialClassChoice(character, classIndex, requires.ChoiceKey)?.Selection is { } stored
                    && LevelUpPlanner.PickedIndexes(stored).Contains(requires.Index, StringComparer.Ordinal);

        var resolved = new List<(PlannedChoice Choice, ChoiceSelection Selection)>();
        var removed = new List<string>();
        foreach (var answer in answers.Values)
        {
            var choice = plan.Choices.FirstOrDefault(c => c.Rule.Key == answer.Key)
                ?? throw AppException.Validation("choices", $"La elección '{OriginChoicesPlanner.ClassPrefix}{answer.Key}' no corresponde a la clase del personaje.");
            if (answer.Replaced.Count > 0)
            {
                throw AppException.Validation("choices", $"En «{choice.Rule.Name}» no se sustituye nada al crear el personaje.");
            }

            var empty = answer.Selected.Count == 0 && answer.Feat is null && answer.Asi is null;
            if (!empty && choice.Requires is { } requires && !Picked(requires))
            {
                throw AppException.Validation("choices", $"«{choice.Rule.Name}» solo se elige con «{requires.Index}» en '{OriginChoicesPlanner.ClassPrefix}{requires.ChoiceKey}'.");
            }

            var selection = empty ? null : LevelChoiceAnswers.Resolve(plan.Sheet, choice, answer);
            if (LevelUpPlanner.InitialClassChoice(character, classIndex, choice.Rule.Key) is { } old && character.RemoveLevelChoice(old, now))
            {
                removed.AddRange(OptionIndexes(old.Selection));
            }

            if (selection is not null)
            {
                resolved.Add((choice, selection));
            }
        }

        LevelChoiceAnswers.EnsureOptionsPickedOnce(resolved, answers);
        foreach (var (choice, selection) in resolved)
        {
            character.RecordChoice(level, classIndex, choice.Rule.Key, selection, now);
        }

        // Choices of an option that is no longer picked stop applying.
        foreach (var nested in plan.Choices.Where(c => c.Requires is not null))
        {
            var parent = LevelUpPlanner.InitialClassChoice(character, classIndex, nested.Requires!.ChoiceKey)?.Selection;
            var stillPicked = parent is not null && LevelUpPlanner.PickedIndexes(parent).Contains(nested.Requires.Index, StringComparer.Ordinal);
            if (!stillPicked && character.LevelChoice(classIndex, level, nested.Rule.Key) is { } stale && character.RemoveLevelChoice(stale, now))
            {
                removed.AddRange(OptionIndexes(stale.Selection));
            }
        }

        await LevelChoiceAnswers.ApplyAllEffectsAsync(catalog, character, classIndex, resolved, removed, now, cancellationToken);
    }

    /// <summary>The options and feats a recorded selection picked (their grants are synced when it goes away).</summary>
    private static IEnumerable<string> OptionIndexes(ChoiceSelection selection) =>
        selection.SetId is null && selection.Feat is null ? [] : LevelUpPlanner.PickedIndexes(selection);

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

        // Languages are optional: the creation wizard may save fewer than offered, never more.
        if (choice.Kind == OriginChoiceKeys.LanguageKind && picks.Count > choice.Choose)
        {
            throw AppException.Validation("choices", $"La elección «{choice.Name}» admite como máximo {choice.Choose} {(choice.Choose == 1 ? "opción" : "opciones")}.");
        }

        if (choice.Kind != OriginChoiceKeys.LanguageKind && picks.Count != choice.Choose)
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

                if (pick.Length > Dnd5eCharacter.IndexMaxLength)
                {
                    throw AppException.Validation("choices", $"«{choice.Name}»: cada valor admite como máximo {Dnd5eCharacter.IndexMaxLength} caracteres.");
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
        List<string>? removed = null;
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
                    case "removedsavingthrows" when property.Value.ValueKind == JsonValueKind.Array:
                        removed = property.Value.EnumerateArray()
                            .Select(e => e.ValueKind == JsonValueKind.String ? e.GetString()?.Trim().ToLowerInvariant() ?? string.Empty : string.Empty)
                            .ToList();
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
            SetId = feat.Definition?.SetId ?? OptionSets.Feats,
            Feat = new ChoiceItem(feat.Index, feat.Name),
            Ability = raised,
            RemovedSavingThrows = RemovedSavingThrowsOf(feat, removed),
        };
    }

    /// <summary>
    /// The saving throws a feat with <c>grants.removeSavingThrows</c> takes away, when the player chooses them
    /// (<c>removedSavingThrows</c>: exactly <c>choose</c> abilities of <c>from</c>); null when nothing is chosen.
    /// </summary>
    internal static List<string>? RemovedSavingThrowsOf(PlannedOption feat, List<string>? removed)
    {
        var rule = feat.Definition?.Grants.RemoveSavingThrows;
        if (rule is null || rule.Fixed.Count > 0)
        {
            if (removed is { Count: > 0 })
            {
                throw AppException.Validation("choices", $"«{feat.Name}» no pide elegir salvaciones que perder.");
            }

            return null;
        }

        var chosen = (removed ?? []).Distinct(StringComparer.Ordinal).ToList();
        if (chosen.Count != rule.Choose
            || chosen.Any(a => !Abilities.IsValid(a) || (rule.From.Count > 0 && !rule.From.Contains(a, StringComparer.Ordinal))))
        {
            var from = rule.From.Count == 0 ? "cualquiera" : string.Join(", ", rule.From);
            throw AppException.Validation("choices", $"«{feat.Name}»: elige {rule.Choose} salvación(es) que pierdes en \"removedSavingThrows\" ({from}).");
        }

        return chosen;
    }
}
