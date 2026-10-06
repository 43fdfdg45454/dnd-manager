using Dnd.Application.Abstractions;
using Dnd.Application.Abstractions.Persistence;
using Dnd.Application.Characters;
using Dnd.Domain.Campaigns;
using Dnd.Domain.Characters;
using FluentValidation;

namespace Dnd.Application.Party;

// DM tools for the table: the party (active characters) at a glance, forced rests and quick adjustments.

/// <param name="Kind">"short" or "long".</param>
/// <param name="CharacterIds">Characters that rest; null or empty = every active character.</param>
public sealed record PartyRestRequest(string Kind, IReadOnlyList<Guid>? CharacterIds);

public static class PartyRestKinds
{
    public const string Short = "short";
    public const string Long = "long";
}

public sealed class PartyRestRequestValidator : AbstractValidator<PartyRestRequest>
{
    public const int MaxCharacters = 100;

    public PartyRestRequestValidator()
    {
        RuleFor(x => x.Kind)
            .Must(k => k is PartyRestKinds.Short or PartyRestKinds.Long)
            .WithMessage("El descanso debe ser short o long.");
        RuleFor(x => x.CharacterIds!)
            .Must(ids => ids.Count <= MaxCharacters).WithMessage($"No se admiten más de {MaxCharacters} personajes.")
            .Must(ids => ids.All(id => id != Guid.Empty)).WithMessage("Indica personajes válidos.")
            .When(x => x.CharacterIds is not null)
            .OverridePropertyName("characterIds");
    }
}

/// <param name="CharacterIds">Characters concerned; null or empty = every active character.</param>
public sealed record PartyLevelRequest(IReadOnlyList<Guid>? CharacterIds);

public sealed class PartyLevelRequestValidator : AbstractValidator<PartyLevelRequest>
{
    public PartyLevelRequestValidator()
    {
        RuleFor(x => x.CharacterIds!)
            .Must(ids => ids.Count <= PartyRestRequestValidator.MaxCharacters)
            .WithMessage($"No se admiten más de {PartyRestRequestValidator.MaxCharacters} personajes.")
            .Must(ids => ids.All(id => id != Guid.Empty)).WithMessage("Indica personajes válidos.")
            .When(x => x.CharacterIds is not null)
            .OverridePropertyName("characterIds");
    }
}

/// <summary>
/// Quick changes of the DM to one character. Null fields do not change.
/// </summary>
/// <param name="HitPointsDelta">Negative = damage (temporary hit points first), positive = healing (up to the maximum).</param>
/// <param name="TemporaryHitPoints">New temporary hit points (replaces the current ones), applied before the delta.</param>
/// <param name="AddConditions">Conditions added (an index already present is left as it is).</param>
/// <param name="RemoveConditions">Indexes of the conditions removed.</param>
/// <param name="HitPointsMax">Overrides the maximum hit points; 0 removes the override.</param>
public sealed record PartyAdjustment(
    Guid CharacterId,
    int? HitPointsDelta = null,
    int? TemporaryHitPoints = null,
    IReadOnlyList<ConditionRequest>? AddConditions = null,
    IReadOnlyList<string>? RemoveConditions = null,
    int? HitPointsMax = null);

public sealed class PartyAdjustmentValidator : AbstractValidator<PartyAdjustment>
{
    public const int MaxHitPointsDelta = 999;

    public PartyAdjustmentValidator()
    {
        RuleFor(x => x.CharacterId).NotEmpty().WithMessage("Indica el personaje.");
        RuleFor(x => x.HitPointsDelta).InclusiveBetween(-MaxHitPointsDelta, MaxHitPointsDelta)
            .WithMessage($"El cambio de puntos de golpe debe estar entre -{MaxHitPointsDelta} y {MaxHitPointsDelta}.");
        RuleFor(x => x.TemporaryHitPoints).InclusiveBetween(0, Character.MaxTemporaryHitPoints)
            .WithMessage($"Los puntos de golpe temporales deben estar entre 0 y {Character.MaxTemporaryHitPoints}.");
        RuleFor(x => x.HitPointsMax).InclusiveBetween(0, OverrideFields.MaxValue)
            .WithMessage($"Los puntos de golpe máximos deben estar entre 0 y {OverrideFields.MaxValue} (0 quita el valor sobrescrito).");
        RuleFor(x => x.AddConditions!)
            .Must(c => c.Count <= Character.MaxConditions)
            .WithMessage($"Un personaje no puede tener más de {Character.MaxConditions} condiciones.")
            .When(x => x.AddConditions is not null)
            .OverridePropertyName("addConditions");
        RuleForEach(x => x.AddConditions)
            .NotNull().WithMessage("La condición no puede ser nula.")
            .ChildRules(c =>
            {
                c.RuleFor(e => e.Index)
                    .Must(i => !string.IsNullOrWhiteSpace(i)).WithMessage("Indica la condición.")
                    .MaximumLength(Character.IndexMaxLength).WithMessage($"La condición no puede superar los {Character.IndexMaxLength} caracteres.");
                c.RuleFor(e => e.Note).MaximumLength(Character.ConditionNoteMaxLength)
                    .WithMessage($"La nota no puede superar los {Character.ConditionNoteMaxLength} caracteres.");
            });
        RuleFor(x => x.RemoveConditions!)
            .Must(c => c.Count <= Character.MaxConditions)
            .WithMessage($"No se pueden quitar más de {Character.MaxConditions} condiciones a la vez.")
            .Must(c => c.All(i => !string.IsNullOrWhiteSpace(i)))
            .WithMessage("Indica la condición que quieres quitar.")
            .When(x => x.RemoveConditions is not null)
            .OverridePropertyName("removeConditions");
    }
}

/// <summary>Body of <c>POST /party/adjust</c>: one adjustment per character, each character at most once.</summary>
public sealed class PartyAdjustmentsValidator : AbstractValidator<List<PartyAdjustment>>
{
    public const int MaxAdjustments = 100;

    public PartyAdjustmentsValidator()
    {
        RuleFor(x => x)
            .Must(a => a.Count is > 0 and <= MaxAdjustments)
            .WithMessage($"Indica entre 1 y {MaxAdjustments} ajustes.")
            .Must(a => a.Where(e => e is not null).Select(e => e.CharacterId).Distinct().Count() == a.Count(e => e is not null))
            .WithMessage("Un personaje no puede aparecer en más de un ajuste.")
            .OverridePropertyName("adjustments");
        RuleForEach(x => x)
            .NotNull().WithMessage("El ajuste no puede ser nulo.")
            .SetValidator(new PartyAdjustmentValidator())
            .OverridePropertyName("adjustments");
    }
}

/// <summary>Loads the party of a campaign for a DM (403 for players, 404 for non-members).</summary>
public sealed class PartyLoader(ICampaignAccess access, ICharacterRepository characters)
{
    /// <summary>Tracked active characters with every child collection.</summary>
    public async Task<IReadOnlyList<Character>> LoadAsync(Guid campaignId, Guid actorUserId, CancellationToken cancellationToken)
    {
        await access.RequireAsync(campaignId, actorUserId, CampaignRole.DM, cancellationToken);
        return await characters.ListActiveWithDetailsAsync(campaignId, cancellationToken);
    }

    /// <summary>The characters of <paramref name="party"/> with the given ids (all when empty); 404 when one is not in it.</summary>
    public static IReadOnlyList<Character> Select(IReadOnlyList<Character> party, IReadOnlyCollection<Guid>? ids)
    {
        if (ids is null || ids.Count == 0)
        {
            return party;
        }

        var byId = party.ToDictionary(c => c.Id);
        return ids.Distinct().Select(id => byId.GetValueOrDefault(id) ?? throw CharacterErrors.CharacterNotFound()).ToList();
    }
}

/// <summary>The active characters of the campaign with their vitals. DMs only.</summary>
public sealed class GetPartyHandler(PartyLoader loader, ICharacterSheetService sheets)
{
    public async Task<PartyDto> HandleAsync(Guid currentUserId, Guid campaignId, CancellationToken cancellationToken = default)
    {
        var party = await loader.LoadAsync(campaignId, currentUserId, cancellationToken);
        return new PartyDto(await sheets.BuildPartyAsync(party, cancellationToken));
    }
}

/// <summary>
/// The DM forces a short or long rest on the party (or some characters) with a single save. A short
/// rest spends no hit dice. The pending rest requests of those characters are cancelled.
/// </summary>
public sealed class PartyRestHandler(
    PartyLoader loader,
    ICharacterSheetService sheets,
    RestRequestLoader restRequests,
    IDiceRoller dice,
    IUnitOfWork unitOfWork,
    ICampaignNotifier notifier,
    IDateTimeProvider clock)
{
    public async Task<PartyDto> HandleAsync(Guid currentUserId, Guid campaignId, PartyRestRequest request, CancellationToken cancellationToken = default)
    {
        var party = await loader.LoadAsync(campaignId, currentUserId, cancellationToken);
        var targets = PartyLoader.Select(party, request.CharacterIds);
        var sheetsById = await sheets.CalculateManyAsync(targets, cancellationToken);
        var now = clock.UtcNow;
        var noHitDice = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var character in targets)
        {
            var sheet = sheetsById[character.Id];
            if (request.Kind == PartyRestKinds.Long)
            {
                character.LongRest(sheet.HitPointsMax, now);
            }
            else
            {
                character.ShortRest(noHitDice, sheet, dice, now);
            }
        }

        // The forced rest answers any rest the players were asking for.
        var cancelled = await restRequests.CancelPendingAsync(targets.Select(c => c.Id).ToList(), currentUserId, now, cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await notifier.NotifyAsync(new CampaignEvent(CampaignEventTypes.PartyRest, campaignId, null, null, now), cancellationToken);
        await RestRequestLoader.NotifyAsync(notifier, cancelled, now, cancellationToken);
        return new PartyDto(await sheets.BuildPartyAsync(party, cancellationToken));
    }
}

/// <summary>
/// The DM adjusts several characters at once with a single save. For each one, in this order: maximum
/// hit points (through the same override path as a sheet edit, recalculating the sheet and capping the
/// current hit points), temporary hit points, damage or healing, and conditions.
/// </summary>
public sealed class PartyAdjustHandler(
    PartyLoader loader,
    ICharacterSheetService sheets,
    IUnitOfWork unitOfWork,
    ICampaignNotifier notifier,
    IDateTimeProvider clock)
{
    public async Task<PartyDto> HandleAsync(Guid currentUserId, Guid campaignId, IReadOnlyList<PartyAdjustment> adjustments, CancellationToken cancellationToken = default)
    {
        var party = await loader.LoadAsync(campaignId, currentUserId, cancellationToken);
        var targets = PartyLoader.Select(party, adjustments.Select(a => a.CharacterId).ToList());
        var sheetsById = (await sheets.CalculateManyAsync(targets, cancellationToken)).ToDictionary();
        var byId = targets.ToDictionary(c => c.Id);
        var now = clock.UtcNow;

        foreach (var adjustment in adjustments)
        {
            var character = byId[adjustment.CharacterId];
            if (adjustment.HitPointsMax is { } hitPointsMax)
            {
                character.ApplySheetEdit(new SheetEdit { Overrides = WithHitPointsMax(character, hitPointsMax) }, now);
                sheetsById[character.Id] = await sheets.RecalculateAsync(character, cancellationToken);
            }

            var maxHp = sheetsById[character.Id].HitPointsMax;
            if (adjustment.TemporaryHitPoints is { } temporary)
            {
                character.ApplyCombatUpdate(new CombatUpdate { TemporaryHitPoints = temporary }, maxHp, now);
            }

            switch (adjustment.HitPointsDelta)
            {
                case < 0 and var damage:
                    character.ApplyDamage(-damage, now);
                    break;
                case > 0 and var healing:
                    character.Heal(healing, maxHp, now);
                    break;
            }

            if (adjustment.AddConditions is { Count: > 0 } || adjustment.RemoveConditions is { Count: > 0 })
            {
                character.ApplyCombatUpdate(new CombatUpdate { Conditions = MergeConditions(character, adjustment) }, maxHp, now);
            }
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        foreach (var character in targets)
        {
            await notifier.CharacterUpdatedAsync(campaignId, character.Id, now, cancellationToken);
        }

        return new PartyDto(await sheets.BuildPartyAsync(party, cancellationToken));
    }

    /// <summary>The character's overrides with <c>hitPointsMax</c> set to <paramref name="value"/> (0 removes it).</summary>
    private static List<OverrideEntry> WithHitPointsMax(Character character, int value)
    {
        var current = character.Overrides.FirstOrDefault(o => o.Field == OverrideFields.HitPointsMax);
        var overrides = character.Overrides
            .Where(o => o.Field != OverrideFields.HitPointsMax)
            .Select(o => new OverrideEntry(o.Field, o.Value, o.Note))
            .ToList();
        if (value > 0)
        {
            overrides.Add(new OverrideEntry(OverrideFields.HitPointsMax, value, current?.Note));
        }

        return overrides;
    }

    /// <summary>Current conditions minus the removed indexes, plus the added ones whose index is not present yet.</summary>
    private static List<CharacterCondition> MergeConditions(Character character, PartyAdjustment adjustment)
    {
        var removed = (adjustment.RemoveConditions ?? []).Select(i => i.Trim()).ToHashSet(StringComparer.Ordinal);
        var result = character.Conditions.Where(c => !removed.Contains(c.Index)).ToList();
        foreach (var added in adjustment.AddConditions ?? [])
        {
            var index = added.Index.Trim();
            if (!result.Any(c => c.Index == index))
            {
                result.Add(new CharacterCondition(index, added.Note));
            }
        }

        return result;
    }
}

/// <summary>
/// The DM grants the next level to the party (or some characters): each one gets
/// <c>PendingLevelUpTo = level + 1</c>, which the player then completes. A pending grant is not
/// accumulated and characters at level 20 are left as they are. The grant can be withdrawn.
/// </summary>
public sealed class PartyLevelHandler(
    PartyLoader loader,
    ICharacterSheetService sheets,
    IUnitOfWork unitOfWork,
    ICampaignNotifier notifier,
    IDateTimeProvider clock)
{
    public async Task<PartyDto> GrantAsync(Guid currentUserId, Guid campaignId, PartyLevelRequest? request, CancellationToken cancellationToken = default)
    {
        var party = await loader.LoadAsync(campaignId, currentUserId, cancellationToken);
        var targets = PartyLoader.Select(party, request?.CharacterIds);
        var now = clock.UtcNow;
        var granted = targets.Where(c => c.GrantLevelUp(currentUserId, now)).ToList();

        await unitOfWork.SaveChangesAsync(cancellationToken);
        foreach (var character in granted)
        {
            var e = new CampaignEvent(CampaignEventTypes.LevelUpGranted, campaignId, character.Id, null, now);
            await notifier.NotifyAsync(e, cancellationToken);
            if (character.OwnerUserId is { } ownerId)
            {
                await notifier.NotifyUserAsync(ownerId, e, cancellationToken);
            }
        }

        return new PartyDto(await sheets.BuildPartyAsync(party, cancellationToken));
    }

    public async Task<PartyDto> RevokeAsync(Guid currentUserId, Guid campaignId, PartyLevelRequest? request, CancellationToken cancellationToken = default)
    {
        var party = await loader.LoadAsync(campaignId, currentUserId, cancellationToken);
        var targets = PartyLoader.Select(party, request?.CharacterIds);
        var now = clock.UtcNow;
        var revoked = targets.Where(c => c.RevokeLevelUp(now)).ToList();

        await unitOfWork.SaveChangesAsync(cancellationToken);
        foreach (var character in revoked)
        {
            await notifier.CharacterUpdatedAsync(campaignId, character.Id, now, cancellationToken);
        }

        return new PartyDto(await sheets.BuildPartyAsync(party, cancellationToken));
    }
}
