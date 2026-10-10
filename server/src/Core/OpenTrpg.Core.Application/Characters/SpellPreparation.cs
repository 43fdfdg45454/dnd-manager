using OpenTrpg.Core.Application.Abstractions;
using OpenTrpg.Core.Application.Abstractions.Persistence;
using OpenTrpg.Core.Application.Common;
using OpenTrpg.Core.Domain.Catalog;
using OpenTrpg.Core.Domain.Characters;
using FluentValidation;

namespace OpenTrpg.Core.Application.Characters;

// Spell preparation (phase 18): GET /characters/{id}/spell-preparation describes what each class can prepare,
// POST /characters/{id}/spell-preparation replaces the prepared spells and POST .../keep keeps the current ones.
// Both clear Character.SpellPreparationPending. A game operation like resources: no DM approval.

/// <summary>A spell as listed by the preparation screen. <see cref="Category"/> is a <c>SpellCategory</c> name.</summary>
public sealed record PreparationSpellDto(
    string Index,
    string Name,
    int Level,
    string School,
    string Category,
    bool Concentration,
    bool Ritual,
    string CastingTime,
    string Source)
{
    public static PreparationSpellDto From(SpellDefinition s) =>
        new(s.Index, s.Name, s.Level, s.School, s.Category.ToString(), s.Concentration, s.Ritual, s.CastingTime, s.Source);
}

/// <summary>
/// One class that prepares spells. <see cref="Prepared"/>: indexes of the spells prepared now (always-prepared
/// ones excluded). <see cref="Candidates"/>: what may be prepared (the class list, or the spellbook for wizards,
/// of the levels with slots of the class; no cantrips, no always-prepared spells).
/// </summary>
/// <param name="Max">Ability modifier + class level (paladin: half the level), minimum 1.</param>
/// <param name="MaxSpellLevel">Highest spell level with slots of the class.</param>
public sealed record SpellPreparationClassDto(
    string ClassIndex,
    string ClassName,
    int Max,
    int MaxSpellLevel,
    IReadOnlyList<PreparationSpellDto> AlwaysPrepared,
    IReadOnlyList<string> Prepared,
    IReadOnlyList<PreparationSpellDto> Candidates);

/// <param name="Pending">The preparation is pending (the app forces the screen).</param>
/// <param name="Reason">"Creation", "LongRest" or "LevelUp"; null when nothing is pending.</param>
/// <param name="CanKeep">The current preparation is still valid and can be kept (<c>POST .../keep</c>).</param>
/// <param name="KeepProblem">Why it cannot be kept (Spanish), when <see cref="CanKeep"/> is false.</param>
public sealed record SpellPreparationDto(
    bool Pending,
    string? Reason,
    bool CanKeep,
    string? KeepProblem,
    IReadOnlyList<SpellPreparationClassDto> Classes);

/// <summary>Body of <c>POST /characters/{id}/spell-preparation</c>: the spells to prepare per class.</summary>
public sealed record PrepareSpellsRequest(IReadOnlyList<PrepareSpellsClassRequest>? Classes);

public sealed record PrepareSpellsClassRequest(string? ClassIndex, IReadOnlyList<string>? Spells);

public sealed class PrepareSpellsRequestValidator : AbstractValidator<PrepareSpellsRequest>
{
    public const int MaxClasses = 20;
    public const int MaxSpellsPerClass = 200;

    public PrepareSpellsRequestValidator()
    {
        RuleFor(x => x.Classes)
            .NotNull().WithMessage("Indica los conjuros preparados de cada clase.")
            .Must(c => c is null || c.Count <= MaxClasses).WithMessage($"No se admiten más de {MaxClasses} clases.");
        RuleForEach(x => x.Classes)
            .Must(c => c is not null && !string.IsNullOrWhiteSpace(c.ClassIndex))
            .WithMessage("Cada clase necesita su classIndex.")
            .Must(c => c?.Spells is not null)
            .WithMessage("Cada clase necesita su lista de conjuros (spells).")
            .Must(c => c?.Spells is null || (c.Spells.Count <= MaxSpellsPerClass && c.Spells.All(s => !string.IsNullOrWhiteSpace(s))))
            .WithMessage($"Cada clase admite como máximo {MaxSpellsPerClass} conjuros, sin índices vacíos.")
            .When(x => x.Classes is not null);
    }
}

/// <summary>What a class can prepare now, computed by <see cref="SpellPreparationPlanner"/>.</summary>
public sealed record PreparationClass(
    SpellcastingValue Casting,
    string ClassName,
    IReadOnlyList<SpellDefinition> AlwaysPrepared,
    IReadOnlyList<string> Prepared,
    IReadOnlyList<SpellDefinition> Candidates)
{
    public string ClassIndex => Casting.ClassIndex;

    public int Max => Casting.PreparedMax ?? 0;

    public bool IsCandidate(string spellIndex) => Candidates.Any(c => c.Index == spellIndex);

    public SpellPreparationClassDto ToDto() => new(
        ClassIndex,
        ClassName,
        Max,
        Casting.MaxSpellLevel,
        AlwaysPrepared.Select(PreparationSpellDto.From).ToList(),
        Prepared,
        Candidates.Select(PreparationSpellDto.From).ToList());
}

/// <summary>The preparation of a character: its sheet, the catalog spells it may need and each preparing class.</summary>
public sealed record PreparationPlan(CharacterSheet Sheet, IReadOnlyDictionary<string, SpellDefinition> Spells, IReadOnlyList<PreparationClass> Classes)
{
    public PreparationClass? Class(string classIndex) => Classes.FirstOrDefault(c => c.ClassIndex == classIndex);

    /// <summary>Why the current preparation cannot be kept (Spanish), or null when it can.</summary>
    public string? KeepProblem()
    {
        foreach (var c in Classes)
        {
            if (c.Prepared.Count == 0)
            {
                if (c.Candidates.Count == 0)
                {
                    continue;
                }

                return $"No tienes conjuros de {c.ClassName} preparados de antes.";
            }

            if (c.Prepared.Count > c.Max)
            {
                return $"Ahora puedes preparar como máximo {c.Max} conjuros de {c.ClassName} y tienes {c.Prepared.Count}.";
            }

            if (c.Prepared.FirstOrDefault(p => !c.IsCandidate(p)) is { } invalid)
            {
                var name = Spells.GetValueOrDefault(invalid)?.Name ?? invalid;
                return $"{name} ya no se puede preparar como {c.ClassName}.";
            }
        }

        return null;
    }
}

/// <summary>
/// Applies the PHB rules of spell preparation (cleric, druid, paladin and wizard; subclasses with their own
/// preparation are not modelled): maximum = ability modifier + class level (paladin: half the level), minimum 1;
/// candidates = the class spell list plus the expanded list of the character's subclass (wizard: only the spellbook)
/// of the levels with slots in the class's own table; cantrips are not prepared and always-prepared spells (domain, oath, circle) do not count.
/// </summary>
public sealed class SpellPreparationPlanner(ICatalogRepository catalog, ICharacterSheetService sheets)
{
    /// <summary>Classes that prepare from their spellbook instead of the whole class list.</summary>
    private static readonly HashSet<string> SpellbookClasses = new(StringComparer.Ordinal) { "wizard" };

    public static bool UsesSpellbook(string classIndex) => SpellbookClasses.Contains(classIndex);

    public async Task<PreparationPlan> BuildAsync(Character character, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(character);
        var sheet = await sheets.CalculateAsync(character, cancellationToken);
        var preparing = sheet.PreparingClasses;
        if (preparing.Count == 0)
        {
            return new PreparationPlan(sheet, new Dictionary<string, SpellDefinition>(StringComparer.Ordinal), []);
        }

        var spells = (await catalog.ListAllSpellsAsync(cancellationToken)).ToDictionary(s => s.Index, StringComparer.Ordinal);
        var classNames = (await catalog.ListClassesByIndexAsync(preparing.Select(p => p.ClassIndex).ToList(), cancellationToken))
            .ToDictionary(c => c.Index, c => c.Name, StringComparer.Ordinal);
        var subclassIndexes = character.Classes.Select(c => c.SubclassIndex).OfType<string>().ToList();
        var subclasses = await catalog.ListSubclassesByIndexAsync(subclassIndexes, cancellationToken);

        var classes = preparing
            .Select(casting =>
            {
                var classIndex = casting.ClassIndex;
                var own = character.Spells.Where(s => s.ClassIndex == classIndex).ToList();
                var always = own.Where(s => s.AlwaysPrepared).Select(s => s.SpellIndex).ToHashSet(StringComparer.Ordinal);
                var subclassIndex = character.Classes.FirstOrDefault(c => c.ClassIndex == classIndex)?.SubclassIndex;
                var expanded = subclasses.FirstOrDefault(s => s.Index == subclassIndex)?.ExpandedSpellIndexes ?? new HashSet<string>();
                bool Leveled(string index, int max) => spells.GetValueOrDefault(index) is { Level: >= 1 } s && s.Level <= max;

                var candidates = UsesSpellbook(classIndex)
                    ? own.Where(s => !s.AlwaysPrepared && Leveled(s.SpellIndex, casting.MaxSpellLevel)).Select(s => spells[s.SpellIndex])
                    : spells.Values.Where(s => (s.ClassIndexes.Contains(classIndex) || expanded.Contains(s.Index)) && s.Level >= 1 && s.Level <= casting.MaxSpellLevel && !always.Contains(s.Index));

                return new PreparationClass(
                    casting,
                    classNames.GetValueOrDefault(classIndex) ?? classIndex,
                    Sorted(always.Select(spells.GetValueOrDefault).OfType<SpellDefinition>().Where(s => s.Level >= 1)),
                    own.Where(s => !s.AlwaysPrepared && s.IsPrepared && Leveled(s.SpellIndex, int.MaxValue))
                        .Select(s => s.SpellIndex)
                        .Order(StringComparer.Ordinal)
                        .ToList(),
                    Sorted(candidates));
            })
            .ToList();

        return new PreparationPlan(sheet, spells, classes);
    }

    /// <summary>
    /// After activating a character: a character that prepares spells and has nothing prepared in some class
    /// must make its first preparation.
    /// </summary>
    public async Task RequireInitialPreparationAsync(Character character, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        var plan = await BuildAsync(character, cancellationToken);
        if (plan.Classes.Any(c => c.Prepared.Count == 0 && c.Candidates.Count > 0))
        {
            character.RequireSpellPreparation(SpellPreparationReason.Creation, now);
        }
    }

    private static List<SpellDefinition> Sorted(IEnumerable<SpellDefinition> spells) =>
        [.. spells.DistinctBy(s => s.Index).OrderBy(s => s.Level).ThenBy(s => s.Name, StringComparer.Ordinal).ThenBy(s => s.Index, StringComparer.Ordinal)];
}

/// <summary>
/// Spell preparation of a character: owner or DM. The owner of an active character only while a preparation is
/// pending (409 otherwise; DMs always). The owner of a draft makes the initial preparation at any time.
/// </summary>
public sealed class SpellPreparationHandler(
    CharacterLoader loader,
    SpellPreparationPlanner planner,
    ICharacterSheetService sheets,
    IUnitOfWork unitOfWork,
    ICampaignNotifier notifier,
    IDateTimeProvider clock)
{
    public const string NotNowMessage = "No es momento de preparar conjuros.";

    public async Task<SpellPreparationDto> GetAsync(Guid currentUserId, Guid characterId, CancellationToken cancellationToken = default)
    {
        var loaded = await loader.LoadAsync(characterId, currentUserId, cancellationToken);
        EnsureCanView(loaded, currentUserId);
        var character = loaded.Character;
        var plan = await planner.BuildAsync(character, cancellationToken);
        var keepProblem = plan.Classes.Count == 0 ? "El personaje no prepara conjuros." : plan.KeepProblem();
        return new SpellPreparationDto(
            character.SpellPreparationPending,
            character.SpellPreparationReason?.ToString(),
            keepProblem is null,
            keepProblem,
            plan.Classes.Select(c => c.ToDto()).ToList());
    }

    public async Task<CharacterDetailDto> PrepareAsync(Guid currentUserId, Guid characterId, PrepareSpellsRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var character = await LoadForChangeAsync(currentUserId, characterId, cancellationToken);
        var plan = await planner.BuildAsync(character, cancellationToken);

        var selections = new List<(PreparationClass Class, List<string> Spells)>();
        foreach (var entry in request.Classes ?? [])
        {
            var classIndex = entry.ClassIndex!.Trim();
            var prepClass = plan.Class(classIndex)
                ?? throw AppException.Validation("classes", $"La clase '{classIndex}' no prepara conjuros (o el personaje no la tiene).");
            if (selections.Any(s => s.Class.ClassIndex == classIndex))
            {
                throw AppException.Validation("classes", $"La clase '{classIndex}' está repetida.");
            }

            var chosen = new List<string>();
            foreach (var raw in entry.Spells ?? [])
            {
                var index = raw.Trim();
                if (chosen.Contains(index, StringComparer.Ordinal))
                {
                    throw AppException.Validation("classes", $"El conjuro '{index}' está repetido.");
                }

                var spell = plan.Spells.GetValueOrDefault(index)
                    ?? throw AppException.Validation("classes", $"El conjuro '{index}' no existe en el catálogo.");
                if (spell.Level == 0)
                {
                    throw AppException.Validation("classes", $"Los trucos no se preparan: {spell.Name}.");
                }

                if (prepClass.AlwaysPrepared.Any(a => a.Index == index))
                {
                    throw AppException.Validation("classes", $"{spell.Name} ya está siempre preparado y no cuenta.");
                }

                if (!prepClass.IsCandidate(index))
                {
                    throw AppException.Validation("classes", UsesSpellbookMessage(prepClass, spell));
                }

                chosen.Add(index);
            }

            if (chosen.Count > prepClass.Max)
            {
                throw AppException.Validation("classes", $"Puedes preparar como máximo {prepClass.Max} conjuros de {prepClass.ClassName}.");
            }

            selections.Add((prepClass, chosen));
        }

        var now = clock.UtcNow;
        foreach (var (prepClass, spells) in selections)
        {
            character.SetPreparedSpells(
                prepClass.ClassIndex,
                spells,
                index => plan.Spells.GetValueOrDefault(index) is { Level: >= 1 },
                keepUnprepared: SpellPreparationPlanner.UsesSpellbook(prepClass.ClassIndex),
                now);
        }

        character.CompleteSpellPreparation(now);
        return await SaveAsync(character, now, cancellationToken);
    }

    public async Task<CharacterDetailDto> KeepAsync(Guid currentUserId, Guid characterId, CancellationToken cancellationToken = default)
    {
        var character = await LoadForChangeAsync(currentUserId, characterId, cancellationToken);
        var plan = await planner.BuildAsync(character, cancellationToken);
        if (plan.KeepProblem() is { } problem)
        {
            throw AppException.Validation("classes", problem);
        }

        var now = clock.UtcNow;
        character.CompleteSpellPreparation(now);
        return await SaveAsync(character, now, cancellationToken);
    }

    private static string UsesSpellbookMessage(PreparationClass prepClass, SpellDefinition spell) =>
        SpellPreparationPlanner.UsesSpellbook(prepClass.ClassIndex)
            ? $"{spell.Name} no está en tu libro de conjuros o es de un nivel sin espacios."
            : spell.Level > prepClass.Casting.MaxSpellLevel
                ? $"{spell.Name} es de nivel {spell.Level} y aún no tienes espacios de ese nivel como {prepClass.ClassName}."
                : $"{spell.Name} no está en la lista de conjuros de {prepClass.ClassName}.";

    private static void EnsureCanView(LoadedCharacter loaded, Guid currentUserId)
    {
        if (!loaded.Character.CanViewSheet(currentUserId, loaded.IsDm))
        {
            throw AppException.Forbidden("Solo el dueño del personaje o un DM pueden preparar sus conjuros.");
        }
    }

    private async Task<Character> LoadForChangeAsync(Guid currentUserId, Guid characterId, CancellationToken cancellationToken)
    {
        var loaded = await loader.LoadAsync(characterId, currentUserId, cancellationToken);
        EnsureCanView(loaded, currentUserId);
        var character = loaded.Character;
        if (!loaded.IsDm && character.Status == CharacterStatus.Active && !character.SpellPreparationPending)
        {
            throw AppException.Conflict(NotNowMessage);
        }

        return character;
    }

    private async Task<CharacterDetailDto> SaveAsync(Character character, DateTimeOffset now, CancellationToken cancellationToken)
    {
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await notifier.CharacterUpdatedAsync(character.CampaignId, character.Id, now, cancellationToken);
        return await sheets.BuildDetailAsync(character, cancellationToken);
    }
}
