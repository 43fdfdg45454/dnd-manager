using OpenTrpg.Core.Application.Abstractions;
using OpenTrpg.Core.Application.Abstractions.Persistence;
using OpenTrpg.Core.Application.Common;
using OpenTrpg.Core.Domain.Characters;
using FluentValidation;

namespace OpenTrpg.Core.Application.Characters;

// Combat tracking: owner and DMs, without approval, in any status.

public sealed record ConditionRequest(string Index, string? Note = null);

/// <summary>Null fields are left unchanged; <see cref="Conditions"/> replaces the whole list when given.</summary>
public sealed record CombatUpdateRequest(
    int? HitPointsCurrent,
    int? TemporaryHitPoints,
    int? DeathSaveSuccesses,
    int? DeathSaveFailures,
    int? ExhaustionLevel,
    IReadOnlyList<ConditionRequest>? Conditions,
    bool? Inspiration);

public sealed class CombatUpdateRequestValidator : AbstractValidator<CombatUpdateRequest>
{
    public CombatUpdateRequestValidator()
    {
        RuleFor(x => x.HitPointsCurrent).GreaterThanOrEqualTo(0).WithMessage("Los puntos de golpe no pueden ser negativos.");
        RuleFor(x => x.TemporaryHitPoints).InclusiveBetween(0, Character.MaxTemporaryHitPoints)
            .WithMessage($"Los puntos de golpe temporales deben estar entre 0 y {Character.MaxTemporaryHitPoints}.");
        RuleFor(x => x.DeathSaveSuccesses).InclusiveBetween(0, Character.MaxDeathSaves)
            .WithMessage($"Las salvaciones contra muerte deben estar entre 0 y {Character.MaxDeathSaves}.");
        RuleFor(x => x.DeathSaveFailures).InclusiveBetween(0, Character.MaxDeathSaves)
            .WithMessage($"Las salvaciones contra muerte deben estar entre 0 y {Character.MaxDeathSaves}.");
        RuleFor(x => x.ExhaustionLevel).InclusiveBetween(0, Character.MaxExhaustionLevel)
            .WithMessage($"El nivel de agotamiento debe estar entre 0 y {Character.MaxExhaustionLevel}.");
        RuleFor(x => x.Conditions!)
            .Must(c => c.Count <= Character.MaxConditions)
            .WithMessage($"Un personaje no puede tener más de {Character.MaxConditions} condiciones.")
            .When(x => x.Conditions is not null)
            .OverridePropertyName("conditions");
        RuleForEach(x => x.Conditions)
            .NotNull().WithMessage("La condición no puede ser nula.")
            .ChildRules(c =>
            {
                c.RuleFor(e => e.Index).NotEmpty().WithMessage("Indica la condición.")
                    .MaximumLength(Character.IndexMaxLength).WithMessage($"La condición no puede superar los {Character.IndexMaxLength} caracteres.");
                c.RuleFor(e => e.Note).MaximumLength(Character.ConditionNoteMaxLength)
                    .WithMessage($"La nota no puede superar los {Character.ConditionNoteMaxLength} caracteres.");
            });
    }
}

/// <param name="SpellIndex">Spell to concentrate on, or null to stop concentrating.</param>
public sealed record ConcentrationRequest(string? SpellIndex);

public sealed class ConcentrationRequestValidator : AbstractValidator<ConcentrationRequest>
{
    public ConcentrationRequestValidator()
    {
        RuleFor(x => x.SpellIndex).MaximumLength(Character.IndexMaxLength)
            .WithMessage($"El índice de conjuro no puede superar los {Character.IndexMaxLength} caracteres.");
    }
}

/// <summary>Damage taken (temporary hit points absorb it first).</summary>
public sealed record DamageRequest(int Amount);

public sealed class DamageRequestValidator : AbstractValidator<DamageRequest>
{
    public const int MaxDamage = 999;

    public DamageRequestValidator()
    {
        RuleFor(x => x.Amount).InclusiveBetween(1, MaxDamage).WithMessage($"El daño debe estar entre 1 y {MaxDamage}.");
    }
}

/// <summary>
/// What a damage meant for one character: the damage, the hit points left and, when it was concentrating, the
/// DC of the Constitution saving throw to keep concentrating (<see cref="ConcentrationCheckDc"/>, max(10, damage / 2))
/// or that the concentration ended because it dropped to 0 hit points (<see cref="ConcentrationEnded"/>).
/// </summary>
public sealed record DamageOutcomeDto(
    Guid CharacterId,
    int Damage,
    int HitPointsCurrent,
    string? ConcentratingOn,
    int? ConcentrationCheckDc,
    bool ConcentrationEnded)
{
    public static DamageOutcomeDto From(Guid characterId, DamageResult result) => new(
        characterId, result.Damage, result.HitPointsCurrent, result.ConcentratingOn, result.ConcentrationCheckDc, result.ConcentrationEnded);
}

/// <summary>Result of <c>POST /characters/{id}/damage</c>: the updated character and the damage outcome.</summary>
public sealed record DamageResultDto(CharacterDetailDto Character, DamageOutcomeDto Outcome);

/// <summary>Uses to spend or restore (default 1). The body is optional.</summary>
public sealed record AmountRequest(int Amount = 1);

public sealed class AmountRequestValidator : AbstractValidator<AmountRequest>
{
    public const int MaxAmount = 999;

    public AmountRequestValidator()
    {
        RuleFor(x => x.Amount).InclusiveBetween(1, MaxAmount).WithMessage($"La cantidad debe estar entre 1 y {MaxAmount}.");
    }
}

/// <summary>Values rolled for a resource that rolls after resting (e.g. [14, 3] for two d20).</summary>
public sealed record ResourceRollsRequest(IReadOnlyList<int>? Values);

public sealed class ResourceRollsRequestValidator : AbstractValidator<ResourceRollsRequest>
{
    public ResourceRollsRequestValidator()
    {
        RuleFor(x => x.Values).NotEmpty().WithMessage("Indica los valores de las tiradas.")
            .Must(v => v is null || v.Count <= RollOnRest.MaxCount).WithMessage($"Como máximo {RollOnRest.MaxCount} tiradas.");
    }
}

/// <param name="Recharge">ShortRest, LongRest, Dawn or Manual.</param>
public sealed record AddResourceRequest(string Name, int Max, string Recharge);

public sealed class AddResourceRequestValidator : AbstractValidator<AddResourceRequest>
{
    public AddResourceRequestValidator()
    {
        RuleFor(x => x.Name)
            .Must(n => !string.IsNullOrWhiteSpace(n)).WithMessage("Indica el nombre del recurso.")
            .Must(n => n is null || n.Trim().Length <= CharacterResource.NameMaxLength)
            .WithMessage($"El nombre del recurso no puede superar los {CharacterResource.NameMaxLength} caracteres.");
        RuleFor(x => x.Max).InclusiveBetween(1, CharacterResource.MaxUses)
            .WithMessage($"Los usos máximos deben estar entre 1 y {CharacterResource.MaxUses}.");
        RuleFor(x => x.Recharge).Must(EnumNames.IsValid<ResourceRecharge>)
            .WithMessage($"La recarga debe ser {EnumNames.Describe<ResourceRecharge>()}.");
    }
}

/// <param name="HitDice">Hit dice to spend per class index (absent: none).</param>
public sealed record ShortRestRequest(IReadOnlyDictionary<string, int>? HitDice);

public sealed class ShortRestRequestValidator : AbstractValidator<ShortRestRequest>
{
    public ShortRestRequestValidator()
    {
        RuleFor(x => x.HitDice!)
            .Must(d => d.All(e => !string.IsNullOrWhiteSpace(e.Key) && e.Value is >= 0 and <= 20))
            .WithMessage("Cada clase debe indicar entre 0 y 20 dados de golpe.")
            .When(x => x.HitDice is not null)
            .OverridePropertyName("hitDice");
    }
}

/// <summary>
/// Loads a character for combat tracking (owner or DM) and saves with the detail as result, publishing
/// <see cref="CampaignEventTypes.CharacterUpdated"/> after the save.
/// </summary>
public sealed class CharacterTracker(
    CharacterLoader loader,
    ICharacterSheetService sheets,
    IUnitOfWork unitOfWork,
    ICampaignNotifier notifier,
    IDateTimeProvider clock)
{
    public async Task<Character> LoadAsync(Guid currentUserId, Guid characterId, CancellationToken cancellationToken) =>
        (await LoadWithRoleAsync(currentUserId, characterId, cancellationToken)).Character;

    /// <summary>Like <see cref="LoadAsync"/>, keeping the actor's campaign role.</summary>
    public async Task<LoadedCharacter> LoadWithRoleAsync(Guid currentUserId, Guid characterId, CancellationToken cancellationToken)
    {
        var loaded = await loader.LoadAsync(characterId, currentUserId, cancellationToken);
        loaded.Character.EnsureCanTrack(currentUserId, loaded.IsDm);
        return loaded;
    }

    public Task<CharacterSheet> SheetAsync(Character character, CancellationToken cancellationToken) =>
        sheets.CalculateAsync(character, cancellationToken);

    public async Task<CharacterDetailDto> SaveAsync(Character character, CancellationToken cancellationToken)
    {
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await NotifyAsync(character, cancellationToken);
        return await sheets.BuildDetailAsync(character, cancellationToken);
    }

    /// <summary>Publishes <see cref="CampaignEventTypes.CharacterUpdated"/> (call after saving).</summary>
    public Task NotifyAsync(Character character, CancellationToken cancellationToken) =>
        notifier.CharacterUpdatedAsync(character.CampaignId, character.Id, clock.UtcNow, cancellationToken);
}

public sealed class UpdateCombatHandler(CharacterTracker tracker, IDateTimeProvider clock)
{
    public async Task<CharacterDetailDto> HandleAsync(Guid currentUserId, Guid characterId, CombatUpdateRequest request, CancellationToken cancellationToken = default)
    {
        var character = await tracker.LoadAsync(currentUserId, characterId, cancellationToken);
        var sheet = await tracker.SheetAsync(character, cancellationToken);
        character.ApplyCombatUpdate(
            new CombatUpdate
            {
                HitPointsCurrent = request.HitPointsCurrent,
                TemporaryHitPoints = request.TemporaryHitPoints,
                DeathSaveSuccesses = request.DeathSaveSuccesses,
                DeathSaveFailures = request.DeathSaveFailures,
                ExhaustionLevel = request.ExhaustionLevel,
                Conditions = request.Conditions?.Select(c => new CharacterCondition(c.Index, c.Note)).ToList(),
                Inspiration = request.Inspiration,
            },
            sheet.HitPointsMax,
            clock.UtcNow);
        return await tracker.SaveAsync(character, cancellationToken);
    }
}

/// <summary>Auto-tracking: the owner or a DM applies damage; reports the concentration check when it applies.</summary>
public sealed class ApplyDamageHandler(CharacterTracker tracker, IDateTimeProvider clock)
{
    public async Task<DamageResultDto> HandleAsync(Guid currentUserId, Guid characterId, DamageRequest request, CancellationToken cancellationToken = default)
    {
        var character = await tracker.LoadAsync(currentUserId, characterId, cancellationToken);
        var result = character.ApplyDamage(request.Amount, clock.UtcNow);
        var detail = await tracker.SaveAsync(character, cancellationToken);
        return new DamageResultDto(detail, DamageOutcomeDto.From(character.Id, result));
    }
}

public sealed class SetConcentrationHandler(CharacterTracker tracker, ICatalogRepository catalog, IDateTimeProvider clock)
{
    public async Task<CharacterDetailDto> HandleAsync(Guid currentUserId, Guid characterId, ConcentrationRequest request, CancellationToken cancellationToken = default)
    {
        var character = await tracker.LoadAsync(currentUserId, characterId, cancellationToken);
        var spellIndex = string.IsNullOrWhiteSpace(request.SpellIndex) ? null : request.SpellIndex.Trim();
        if (spellIndex is not null && await catalog.GetSpellAsync(spellIndex, cancellationToken) is null)
        {
            throw AppException.Validation("spellIndex", $"El conjuro '{spellIndex}' no existe en el catálogo.");
        }

        character.SetConcentration(spellIndex, clock.UtcNow);
        return await tracker.SaveAsync(character, cancellationToken);
    }
}

/// <summary>Spends (or restores) spell slots of a level; level 0 = pact slots.</summary>
public sealed class SpellSlotHandler(CharacterTracker tracker, IDateTimeProvider clock)
{
    public async Task<CharacterDetailDto> SpendAsync(Guid currentUserId, Guid characterId, int level, AmountRequest? request, CancellationToken cancellationToken = default)
    {
        var character = await tracker.LoadAsync(currentUserId, characterId, cancellationToken);
        var sheet = await tracker.SheetAsync(character, cancellationToken);
        character.SpendSpellSlot(level, request?.Amount ?? 1, sheet.SpellSlotMax(level), clock.UtcNow);
        return await tracker.SaveAsync(character, cancellationToken);
    }

    public async Task<CharacterDetailDto> RestoreAsync(Guid currentUserId, Guid characterId, int level, AmountRequest? request, CancellationToken cancellationToken = default)
    {
        var character = await tracker.LoadAsync(currentUserId, characterId, cancellationToken);
        character.RestoreSpellSlot(level, request?.Amount ?? 1, clock.UtcNow);
        return await tracker.SaveAsync(character, cancellationToken);
    }
}

/// <summary>
/// Limited-use resources: spend, restore, add a manual one, delete a manual one. Automatic class
/// resources (rage, ki, lay on hands...) only come back with rests or by the DM's hand: a player
/// restoring one gets 403, except sorcery points, which Font of Magic converts from spell slots, and Tides of Chaos
/// (keys ending in <see cref="TidesOfChaosSuffix"/>), which comes back after a wild magic surge.
/// </summary>
public sealed class ResourceHandler(CharacterTracker tracker, IUnitOfWork unitOfWork, IDateTimeProvider clock)
{
    public const string AutoResourceRestoreForbidden = "Este recurso solo se recupera descansando o por decisión del DM.";

    /// <summary>Key suffix of Tides of Chaos resources (subclass feature resources of content packs).</summary>
    public const string TidesOfChaosSuffix = "tides-of-chaos";

    /// <summary>Automatic resources a player may restore by themselves.</summary>
    public static bool PlayerMayRestore(string? key) =>
        key == ClassResourceRules.SorceryPoints || (key is not null && key.EndsWith(TidesOfChaosSuffix, StringComparison.Ordinal));

    public async Task<CharacterDetailDto> SpendAsync(Guid currentUserId, Guid characterId, Guid resourceId, AmountRequest? request, CancellationToken cancellationToken = default)
    {
        var character = await tracker.LoadAsync(currentUserId, characterId, cancellationToken);
        character.SpendResource(resourceId, request?.Amount ?? 1, clock.UtcNow);
        return await tracker.SaveAsync(character, cancellationToken);
    }

    public async Task<CharacterDetailDto> RestoreAsync(Guid currentUserId, Guid characterId, Guid resourceId, AmountRequest? request, CancellationToken cancellationToken = default)
    {
        var loaded = await tracker.LoadWithRoleAsync(currentUserId, characterId, cancellationToken);
        var character = loaded.Character;
        var resource = character.Resources.FirstOrDefault(r => r.Id == resourceId);
        if (resource is { IsAuto: true } && !PlayerMayRestore(resource.Key) && !loaded.IsDm)
        {
            throw AppException.Forbidden(AutoResourceRestoreForbidden);
        }

        character.RestoreResource(resourceId, request?.Amount ?? 1, clock.UtcNow);
        return await tracker.SaveAsync(character, cancellationToken);
    }

    public async Task<CharacterResourceDto> AddAsync(Guid currentUserId, Guid characterId, AddResourceRequest request, CancellationToken cancellationToken = default)
    {
        var character = await tracker.LoadAsync(currentUserId, characterId, cancellationToken);
        var resource = character.AddManualResource(request.Name, request.Max, EnumNames.Parse<ResourceRecharge>(request.Recharge), clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await tracker.NotifyAsync(character, cancellationToken);
        return CharacterResourceDto.From(resource);
    }

    /// <summary>Stores the dice rolled after a rest for a resource that asks for them (owner or DM, without approval).</summary>
    public async Task<CharacterDetailDto> RecordRollsAsync(Guid currentUserId, Guid characterId, Guid resourceId, ResourceRollsRequest request, CancellationToken cancellationToken = default)
    {
        var character = await tracker.LoadAsync(currentUserId, characterId, cancellationToken);
        character.RecordResourceRolls(resourceId, request.Values ?? [], clock.UtcNow);
        return await tracker.SaveAsync(character, cancellationToken);
    }

    public async Task DeleteAsync(Guid currentUserId, Guid characterId, Guid resourceId, CancellationToken cancellationToken = default)
    {
        var character = await tracker.LoadAsync(currentUserId, characterId, cancellationToken);
        character.RemoveManualResource(resourceId, clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await tracker.NotifyAsync(character, cancellationToken);
    }
}

/// <summary>
/// Direct rests: DMs only (players ask for one with a rest request). A direct rest also cancels the
/// character's pending rest request, which it makes moot.
/// </summary>
public sealed class RestHandler(
    CharacterLoader loader,
    CharacterTracker tracker,
    RestRequestLoader restRequests,
    ICampaignNotifier notifier,
    IDiceRoller dice,
    CompanionPlanner companions,
    IDateTimeProvider clock)
{
    public async Task<CharacterDetailDto> ShortRestAsync(Guid currentUserId, Guid characterId, ShortRestRequest? request, CancellationToken cancellationToken = default)
    {
        var character = await LoadForDmAsync(currentUserId, characterId, cancellationToken);
        var sheet = await tracker.SheetAsync(character, cancellationToken);
        var hitDice = request?.HitDice?.ToDictionary(d => d.Key, d => d.Value, StringComparer.Ordinal)
            ?? new Dictionary<string, int>(StringComparer.Ordinal);
        var now = clock.UtcNow;
        character.ShortRest(hitDice, sheet, dice, now);
        return await SaveAsync(currentUserId, character, now, cancellationToken);
    }

    public async Task<CharacterDetailDto> LongRestAsync(Guid currentUserId, Guid characterId, CancellationToken cancellationToken = default)
    {
        var character = await LoadForDmAsync(currentUserId, characterId, cancellationToken);
        var sheet = await tracker.SheetAsync(character, cancellationToken);
        var now = clock.UtcNow;
        character.LongRest(sheet, now);
        await companions.RestoreAfterLongRestAsync([character], now, cancellationToken);
        return await SaveAsync(currentUserId, character, now, cancellationToken);
    }

    private async Task<Character> LoadForDmAsync(Guid currentUserId, Guid characterId, CancellationToken cancellationToken)
    {
        var loaded = await loader.LoadAsync(characterId, currentUserId, cancellationToken);
        if (!loaded.IsDm)
        {
            throw AppException.Forbidden(RestRequestErrors.AskTheDm);
        }

        return loaded.Character;
    }

    private async Task<CharacterDetailDto> SaveAsync(Guid currentUserId, Character character, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var cancelled = await restRequests.CancelPendingAsync([character.Id], currentUserId, now, cancellationToken);
        var detail = await tracker.SaveAsync(character, cancellationToken);
        await RestRequestLoader.NotifyAsync(notifier, cancelled, now, cancellationToken);
        return detail;
    }
}
