using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using OpenTrpg.Core.Application.Abstractions;
using OpenTrpg.Core.Application.Abstractions.Persistence;
using OpenTrpg.Core.Application.ChangeRequests;
using OpenTrpg.Core.Application.Common;
using OpenTrpg.Core.Domain.Catalog;
using OpenTrpg.Core.Domain.Characters;
using FluentValidation;
using OpenTrpg.Core.Application;
using OpenTrpg.Core.Application.Characters;
using OpenTrpg.Systems.Dnd5e.Application;
using OpenTrpg.Systems.Dnd5e.Application.Abstractions;
using OpenTrpg.Systems.Dnd5e.Application.Abstractions.Persistence;
using OpenTrpg.Systems.Dnd5e.Application.Catalog;
using OpenTrpg.Systems.Dnd5e.Application.Characters;
using OpenTrpg.Systems.Dnd5e.Domain.Catalog;
using OpenTrpg.Systems.Dnd5e.Domain.Characters;

namespace OpenTrpg.Systems.Dnd5e.Application.Characters;

// Companions (phase 25, block 6; several and of any creature type since phase 35B3): creatures of the catalog chosen
// through a subclass feature with a companion rule.

/// <summary>The beast (index of the beast catalog) and the name of the companion.</summary>
public sealed record SetCompanionRequest(string BeastIndex, string Name);

public sealed class SetCompanionRequestValidator : AbstractValidator<SetCompanionRequest>
{
    public SetCompanionRequestValidator()
    {
        RuleFor(x => x.BeastIndex)
            .Must(b => !string.IsNullOrWhiteSpace(b)).WithMessage("Indica la bestia del compañero.")
            .MaximumLength(CharacterCompanion.BeastIndexMaxLength).WithMessage("La bestia no es válida.");
        RuleFor(x => x.Name)
            .Must(n => !string.IsNullOrWhiteSpace(n)).WithMessage("Indica el nombre del compañero.")
            .Must(n => n is null || n.Trim().Length <= CharacterCompanion.NameMaxLength)
            .WithMessage($"El nombre del compañero no puede superar los {CharacterCompanion.NameMaxLength} caracteres.");
    }
}

/// <summary>Hit points of the companion: a change (<see cref="Delta"/>, negative for damage) or the new value (<see cref="Current"/>).</summary>
public sealed record CompanionHpRequest(int? Delta, int? Current);

public sealed class CompanionHpRequestValidator : AbstractValidator<CompanionHpRequest>
{
    public CompanionHpRequestValidator()
    {
        RuleFor(x => x).Must(x => (x.Delta is null) != (x.Current is null))
            .WithMessage("Indica un cambio (delta) o el valor actual (current) de los puntos de golpe.")
            .OverridePropertyName("delta");
        RuleFor(x => x.Delta).InclusiveBetween(-CharacterCompanion.MaxHitPoints, CharacterCompanion.MaxHitPoints)
            .WithMessage($"El cambio debe estar entre -{CharacterCompanion.MaxHitPoints} y {CharacterCompanion.MaxHitPoints}.");
        RuleFor(x => x.Current).InclusiveBetween(0, CharacterCompanion.MaxHitPoints)
            .WithMessage($"Los puntos de golpe deben estar entre 0 y {CharacterCompanion.MaxHitPoints}.");
    }
}

/// <summary>Outcome of choosing the companion: applied (<see cref="Character"/>) or sent to the DM (<see cref="ChangeRequest"/>).</summary>
public sealed record CompanionResult(CharacterDetailDto? Character, ChangeRequestDto? ChangeRequest);

/// <summary>
/// The rules of the companion: the feature a character has reached, the beasts it allows, the statblock and the DTOs, and
/// applying a choice (directly or from an approved change request).
/// </summary>
public sealed class CompanionPlanner(ICatalogRepository catalog, IBeastCatalog beasts, ICharacterCompanionRepository companions, IServiceProvider services)
{
    /// <summary>Rule used for a companion whose feature the character no longer has: the beast as it is.</summary>
    private static readonly CompanionRule NoFeature = new(CompanionRule.MaxChallengeRatingLimit, [], null, false, false);

    public static AppException NoCompanionFeature() =>
        AppException.Conflict("El personaje no tiene un rasgo de compañero animal a su nivel.");

    public static AppException NoCompanion() => AppException.NotFound("El personaje no tiene compañero animal.");

    /// <summary>
    /// The character's level and ability modifiers for the hit points formula of the rule (the sheet is calculated only when
    /// the formula uses a modifier).
    /// </summary>
    public async Task<CompanionContext> ContextAsync(Dnd5eCharacter character, CompanionGrant? grant, CancellationToken cancellationToken)
    {
        if (grant?.Rule.HitPointsTerms?.Any(t => t.Symbol.StartsWith("mod:", StringComparison.Ordinal)) != true)
        {
            return new CompanionContext(character.TotalLevel, _ => 0);
        }

        // Resolved here: the sheet service itself depends on this planner.
        var sheet = await services.GetRequiredService<ICharacterSheetService>().CalculateAsync(character, cancellationToken);
        return Context(character, sheet);
    }

    /// <summary>The context of the companion formulas from a calculated sheet.</summary>
    public static CompanionContext Context(Dnd5eCharacter character, CharacterSheet sheet)
    {
        var modifiers = sheet.AbilityModifiers;
        return new CompanionContext(character.TotalLevel, a => modifiers.GetValueOrDefault(a));
    }

    /// <summary>The companion feature the character has reached, or null.</summary>
    public async Task<CompanionGrant?> GrantAsync(Dnd5eCharacter character, CancellationToken cancellationToken)
    {
        var classes = character.Classes.Select(c => c.ClassIndex).Distinct(StringComparer.Ordinal).ToList();
        var subclasses = character.Classes.Select(c => c.SubclassIndex).OfType<string>().Distinct(StringComparer.Ordinal).ToList();
        var features = await catalog.ListFeatureEffectsAsync(classes, subclasses, cancellationToken);
        return CompanionGrants.Find(character, features);
    }

    /// <summary>
    /// The creature, if the catalog has it and the rule allows it (a beast of the filter, or one of the rule's closed list of
    /// any type); a validation error on <c>beastIndex</c> otherwise.
    /// </summary>
    public async Task<BeastDto> RequireBeastAsync(CompanionRule rule, string beastIndex, CancellationToken cancellationToken)
    {
        var index = beastIndex.Trim();
        var beast = await BeastAsync(index, cancellationToken);
        var inScope = beast is not null && (await beasts.ListAsync(cancellationToken)).Any(b => b.Index == beast.Index);
        if (beast is null || !inScope || (rule.CreatureIndexes.Count == 0 && !beast.IsBeast))
        {
            throw AppException.Validation("beastIndex", $"La {(rule.CreatureIndexes.Count == 0 ? "bestia" : "criatura")} '{index}' no existe en el catálogo.");
        }

        if (!rule.AllowsCreature(beast.Index, beast.IsBeast, beast.ChallengeRating, beast.Size))
        {
            throw AppException.Validation(
                "beastIndex",
                rule.CreatureIndexes.Count > 0
                    ? $"{beast.Name} no puede ser tu compañero: no está en la lista del rasgo."
                    : $"{beast.Name} no puede ser tu compañero: {DescribeFilter(rule)}.");
        }

        return beast;
    }

    /// <summary>Maximum hit points of a companion of that creature (they do not depend on the proficiency bonus).</summary>
    public static int MaxHitPoints(BeastDto beast, CompanionGrant? grant, int? maxOverride = null, CompanionContext? context = null) =>
        CompanionCalculator.Calculate(ToDomain(beast), grant?.Rule ?? NoFeature, grant?.ClassIndex ?? string.Empty, grant?.ClassLevel ?? 0, 0, maxOverride, context)
            .HitPointsMax.Total;

    /// <summary>Maximum hit points of an existing companion (its stored hit points when the creature is gone from the catalog).</summary>
    public async Task<int> MaxHitPointsAsync(CharacterCompanion companion, CompanionGrant? grant, CompanionContext? context, CancellationToken cancellationToken) =>
        await BeastAsync(companion.BeastIndex, cancellationToken) is { } beast
            ? MaxHitPoints(beast, grant, companion.HitPointsMaxOverride, context)
            : companion.HitPointsMaxOverride ?? companion.HitPointsCurrent;

    /// <summary>
    /// Creates a companion (<paramref name="add"/>, or when the character has none), renames one or changes its creature
    /// (back to full hit points): the one of <paramref name="companionId"/>, or the first. The caller has checked who may do
    /// it; the creature, the number of companions and the challenge rating budget are checked here against the feature the
    /// character has reached.
    /// </summary>
    public async Task<CharacterCompanion> ApplyAsync(
        Dnd5eCharacter character,
        string beastIndex,
        string name,
        DateTimeOffset now,
        CancellationToken cancellationToken,
        Guid? companionId = null,
        bool add = false)
    {
        var grant = await GrantAsync(character, cancellationToken) ?? throw NoCompanionFeature();
        var beast = await RequireBeastAsync(grant.Rule, beastIndex, cancellationToken);
        var context = await ContextAsync(character, grant, cancellationToken);
        var all = await companions.ListByCharacterAsync(character.Id, cancellationToken);
        var existing = add ? null
            : companionId is { } id ? all.FirstOrDefault(c => c.Id == id) ?? throw NoCompanion()
            : all.FirstOrDefault();
        await EnsureRoomAsync(grant, character, all.Where(c => c != existing).ToList(), beast, cancellationToken);
        if (existing is null)
        {
            var created = CharacterCompanion.Create(character.Id, beast.Index, name, MaxHitPoints(beast, grant, null, context), now);
            companions.Add(created);
            return created;
        }

        if (string.Equals(existing.BeastIndex, beast.Index, StringComparison.Ordinal))
        {
            existing.Rename(name, now);
        }
        else
        {
            existing.ChangeBeast(beast.Index, name, MaxHitPoints(beast, grant, existing.HitPointsMaxOverride, context), now);
        }

        return existing;
    }

    /// <summary>
    /// 409 when the character already has as many companions as the feature allows; 400 on <c>beastIndex</c> when the
    /// challenge ratings of <paramref name="others"/> plus the new creature exceed the budget of the class level.
    /// </summary>
    private async Task EnsureRoomAsync(CompanionGrant grant, Dnd5eCharacter character, IReadOnlyList<CharacterCompanion> others, BeastDto beast, CancellationToken cancellationToken)
    {
        var max = grant.Rule.MaxCountFor(Domain.Rules.AbilityRules.ProficiencyBonus(character.TotalLevel));
        if (others.Count >= max)
        {
            throw AppException.Conflict(max == 1 ? "El personaje ya tiene su compañero." : $"El personaje ya tiene {max} compañeros, el máximo de su rasgo.");
        }

        if (grant.Rule.ChallengeBudgetAt(grant.ClassLevel) is { } budget)
        {
            var total = beast.ChallengeRating;
            foreach (var other in others)
            {
                total += (await BeastAsync(other.BeastIndex, cancellationToken))?.ChallengeRating ?? 0;
            }

            if (total > budget + 1e-9)
            {
                throw AppException.Validation(
                    "beastIndex",
                    $"El VD total de tus compañeros ({BeastDto.FormatChallengeRating(total)}) superaría el máximo de {BeastDto.FormatChallengeRating(budget)} a tu nivel.");
            }
        }
    }

    /// <summary>Applies an approved <see cref="Dnd5eChangeRequestTypes.Companion"/> request.</summary>
    public Task ApplyApprovedAsync(Dnd5eCharacter character, string payloadJson, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var payload = CompanionPayload.Parse(payloadJson)
            ?? throw AppException.Validation("payload", "El contenido de la solicitud no es un compañero válido.");
        return ApplyAsync(character, payload.BeastIndex, payload.Name, now, cancellationToken, payload.CompanionId);
    }

    /// <summary>A long rest brings the companions of the given characters back to full hit points.</summary>
    public async Task RestoreAfterLongRestAsync(IReadOnlyList<Dnd5eCharacter> characters, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var list = await companions.ListByCharactersAsync(characters.Select(c => c.Id).ToList(), cancellationToken);
        foreach (var companion in list)
        {
            var character = characters.First(c => c.Id == companion.CharacterId);
            var grant = await GrantAsync(character, cancellationToken);
            companion.RestoreHitPoints(await MaxHitPointsAsync(companion, grant, await ContextAsync(character, grant, cancellationToken), cancellationToken), now);
        }
    }

    /// <summary>The feature as the detail shows it, with what it allows at the character's level.</summary>
    public async Task<CompanionFeatureDto> FeatureDtoAsync(CompanionGrant grant, int proficiencyBonus, CancellationToken cancellationToken)
    {
        var rule = grant.Rule;
        var creatures = new List<CompanionCreatureDto>();
        foreach (var index in rule.CreatureIndexes)
        {
            if (await BeastAsync(index, cancellationToken) is { } creature)
            {
                creatures.Add(new CompanionCreatureDto(creature.Index, creature.Name, creature.Type, creature.Size, creature.ChallengeRating, creature.ChallengeRatingText));
            }
        }

        return FeatureDto(grant) with
        {
            CreatureIndexes = rule.CreatureIndexes,
            Creatures = creatures,
            MaxCount = rule.MaxCountFor(proficiencyBonus),
            ChallengeBudget = rule.ChallengeBudgetAt(grant.ClassLevel),
            ChallengeBudgetText = rule.ChallengeBudgetAt(grant.ClassLevel) is { } budget ? BeastDto.FormatChallengeRating(budget) : null,
            Bonuses = rule.Bonuses.IsEmpty ? null : new CompanionBonusesDto(rule.Bonuses.ArmorClass, rule.Bonuses.SavingThrows, rule.Bonuses.Attack, rule.Bonuses.Damage),
            Features = rule.FeaturesAt(grant.ClassLevel).Select(f => new CompanionFeatureTextDto(f.Level, f.Text)).ToList(),
        };
    }

    /// <summary>The feature as the detail shows it (without the creatures of its list).</summary>
    public static CompanionFeatureDto FeatureDto(CompanionGrant grant) => new(
        grant.Feature.Index,
        grant.Feature.Name,
        grant.ClassIndex,
        grant.ClassLevel,
        grant.Rule.MaxChallengeRating,
        BeastDto.FormatChallengeRating(grant.Rule.MaxChallengeRating),
        grant.Rule.Sizes,
        grant.Rule.HitPointsText,
        grant.Rule.ProficiencyBonusFromCharacter,
        grant.Rule.AttackBonusFromCharacter);

    /// <summary>The companion with its statblock recalculated with the character's <paramref name="proficiencyBonus"/>.</summary>
    public async Task<CharacterCompanionDto> BuildDtoAsync(CharacterCompanion companion, CompanionGrant? grant, int proficiencyBonus, CancellationToken cancellationToken, CompanionContext? context = null)
    {
        var found = await BeastAsync(companion.BeastIndex, cancellationToken);
        var beast = found ?? new BeastDto(
            companion.BeastIndex, companion.BeastIndex, string.Empty, string.Empty, 0, "0", 0, 2, 10, null,
            Math.Max(1, companion.HitPointsCurrent), string.Empty, null,
            new Dictionary<string, int>(), new Dictionary<string, int>(), new Dictionary<string, int>(), new Dictionary<string, int>(),
            new Dictionary<string, string>(), 10, string.Empty, [], [], [], [], [], [], null);
        var rule = grant?.Rule ?? NoFeature;
        var block = CompanionCalculator.Calculate(
            ToDomain(beast), rule, grant?.ClassIndex ?? string.Empty, grant?.ClassLevel ?? 0, proficiencyBonus, companion.HitPointsMaxOverride, context);

        var breakdowns = new Dictionary<string, ValueBreakdownDto>(StringComparer.Ordinal)
        {
            ["armorClass"] = ValueBreakdownDto.From(block.ArmorClass),
            ["hitPointsMax"] = ValueBreakdownDto.From(block.HitPointsMax),
        };
        foreach (var (ability, value) in block.SavingThrows)
        {
            breakdowns[$"save.{ability}"] = ValueBreakdownDto.From(value);
        }

        foreach (var (skill, value) in block.Skills)
        {
            breakdowns[$"skill.{skill}"] = ValueBreakdownDto.From(value);
        }

        var passive = beast.PassivePerception
            + (rule.ProficiencyBonusFromCharacter && beast.Skills.ContainsKey("perception") ? proficiencyBonus : 0);
        var max = block.HitPointsMax.Total;
        return new CharacterCompanionDto(
            companion.Id,
            companion.BeastIndex,
            beast.Name,
            companion.Name,
            beast.Size,
            beast.ChallengeRatingText,
            companion.CurrentWithin(max),
            max,
            companion.HitPointsMaxOverride,
            block.ArmorClass.Total,
            beast.Speeds,
            beast.Abilities,
            block.SavingThrows.ToDictionary(s => s.Key, s => s.Value.Total, StringComparer.Ordinal),
            block.Skills.ToDictionary(s => s.Key, s => s.Value.Total, StringComparer.Ordinal),
            beast.Senses,
            passive,
            beast.Traits,
            block.Attacks
                .Select(a => new CompanionAttackDto(
                    a.Name,
                    a.Description,
                    a.AttackBonus?.Total,
                    a.AttackBonus is null ? null : ValueBreakdownDto.From(a.AttackBonus),
                    a.Damage.Select(d => new CompanionDamageDto(d.Dice, d.Type)).ToList(),
                    a.DamageBonus is null ? null : ValueBreakdownDto.From(a.DamageBonus),
                    a.IsMultiattack))
                .ToList(),
            breakdowns,
            found is null)
        {
            Type = beast.Type,
            Features = grant is null ? [] : rule.FeaturesAt(grant.ClassLevel).Select(f => new CompanionFeatureTextDto(f.Level, f.Text)).ToList(),
        };
    }

    /// <summary>The creature of the catalog with that index (whatever its pack), or null.</summary>
    public Task<BeastDto?> BeastAsync(string index, CancellationToken cancellationToken) =>
        beasts.FindAsync(index.Trim().ToLowerInvariant(), cancellationToken);

    private static string DescribeFilter(CompanionRule rule)
    {
        var cr = $"VD {BeastDto.FormatChallengeRating(rule.MaxChallengeRating)} o menos";
        return rule.Sizes.Count == 0 ? cr : $"{cr} y tamaño {string.Join(" o ", rule.Sizes)}";
    }

    private static CompanionBeast ToDomain(BeastDto beast) => new(
        beast.Index,
        beast.Name,
        beast.Size,
        beast.ChallengeRating,
        beast.ArmorClass,
        beast.HitPoints,
        beast.SavingThrows,
        beast.Skills,
        beast.Actions
            .Select(a => new CompanionBeastAction(a.Name, a.Description, a.AttackBonus, a.Damage.Select(d => new CompanionDamage(d.Dice, d.Type)).ToList(), a.IsMultiattack))
            .ToList())
    {
        Abilities = beast.Abilities,
    };
}

/// <summary>Payload (and snapshot) of a <see cref="Dnd5eChangeRequestTypes.Companion"/> request; <see cref="CompanionId"/> names the companion changed (null: the first).</summary>
public sealed record CompanionPayload(string BeastIndex, string BeastName, string Name, Guid? CompanionId = null)
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public string ToJson() => JsonSerializer.Serialize(this, Options);

    public static CompanionPayload? Parse(string json)
    {
        try
        {
            var payload = JsonSerializer.Deserialize<CompanionPayload>(json, Options);
            return payload is { BeastIndex.Length: > 0, Name.Length: > 0 } ? payload : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

/// <summary>
/// <c>PUT /characters/{id}/companion</c>: the owner or a DM. Choosing the first companion and renaming it apply directly; a
/// player changing the beast of an active character creates a <see cref="Dnd5eChangeRequestTypes.Companion"/> request, which DMs
/// (and the owner of a draft) skip.
/// </summary>
public sealed class SetCompanionHandler(
    Dnd5eCharacterLoader loader,
    CompanionPlanner planner,
    ICharacterCompanionRepository companions,
    ICharacterSheetService sheets,
    IChangeRequestRepository changeRequests,
    IUnitOfWork unitOfWork,
    ICampaignNotifier notifier,
    IDateTimeProvider clock)
{
    /// <param name="companionId">The companion changed (<c>PUT …/companions/{id}</c>); null for the first one (<c>PUT …/companion</c>).</param>
    /// <param name="add">A new companion (<c>POST …/companions</c>), always applied directly within the feature's limits.</param>
    public async Task<CompanionResult> HandleAsync(
        Guid currentUserId,
        Guid characterId,
        SetCompanionRequest request,
        CancellationToken cancellationToken = default,
        Guid? companionId = null,
        bool add = false)
    {
        var loaded = await loader.LoadAsync(characterId, currentUserId, cancellationToken);
        var character = loaded.Character;
        character.EnsureCanTrack(currentUserId, loaded.IsDm);
        var grant = await planner.GrantAsync(character, cancellationToken) ?? throw CompanionPlanner.NoCompanionFeature();
        var beast = await planner.RequireBeastAsync(grant.Rule, request.BeastIndex, cancellationToken);
        var name = CharacterCompanion.NormalizeName(request.Name);
        var all = await companions.ListByCharacterAsync(character.Id, cancellationToken);
        var existing = add ? null
            : companionId is { } id ? all.FirstOrDefault(c => c.Id == id) ?? throw CompanionPlanner.NoCompanion()
            : all.FirstOrDefault();
        var now = clock.UtcNow;

        var direct = existing is null
            || string.Equals(existing.BeastIndex, beast.Index, StringComparison.Ordinal)
            || character.ResolveSheetEdit(currentUserId, loaded.IsDm) == SheetEditMode.Direct;
        if (direct)
        {
            await planner.ApplyAsync(character, beast.Index, name, now, cancellationToken, existing?.Id, add || existing is null);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await notifier.CharacterUpdatedAsync(character.CampaignId, character.Id, now, cancellationToken);
            return new CompanionResult(await sheets.BuildDetailAsync(character, cancellationToken), null);
        }

        var before = new CompanionPayload(existing!.BeastIndex, (await planner.BeastAsync(existing.BeastIndex, cancellationToken))?.Name ?? existing.BeastIndex, existing.Name);
        var changeRequest = ChangeRequest.Create(
            character.CampaignId,
            character.Id,
            currentUserId,
            Dnd5eChangeRequestTypes.Companion,
            new CompanionPayload(beast.Index, beast.Name, name, existing.Id).ToJson(),
            now,
            before.ToJson());
        changeRequests.Add(changeRequest);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await notifier.ChangeRequestUpdatedAsync(character.CampaignId, character.Id, changeRequest.Id, now, cancellationToken);

        var view = (await changeRequests.ListViewsAsync(new ChangeRequestQuery(Id: changeRequest.Id), cancellationToken)).Single();
        return new CompanionResult(null, ChangeRequestDto.From(view));
    }
}

/// <summary>Companion hit points (owner or DM, without approval) and its removal (DMs only).</summary>
public sealed class CompanionTrackingHandler(
    CharacterTracker tracker,
    CompanionPlanner planner,
    ICharacterCompanionRepository companions,
    IUnitOfWork unitOfWork,
    IDateTimeProvider clock)
{
    /// <param name="companionId">The companion (<c>…/companions/{id}/hp</c>); null for the first one (<c>…/companion/hp</c>).</param>
    public async Task<CharacterDetailDto> TrackHitPointsAsync(Guid currentUserId, Guid characterId, CompanionHpRequest request, CancellationToken cancellationToken = default, Guid? companionId = null)
    {
        var character = await tracker.LoadAsync(currentUserId, characterId, cancellationToken);
        var companion = await FindAsync(character.Id, companionId, cancellationToken);
        var grant = await planner.GrantAsync(character, cancellationToken);
        var max = await planner.MaxHitPointsAsync(companion, grant, await planner.ContextAsync(character, grant, cancellationToken), cancellationToken);
        companion.TrackHitPoints(request.Delta, request.Current, max, clock.UtcNow);
        return await tracker.SaveAsync(character, cancellationToken);
    }

    /// <summary>
    /// Removes a companion: <c>DELETE …/companions/{id}</c> by the owner or a DM (dismissing a companion is tracked without
    /// approval); <c>DELETE …/companion</c> (the first one) only by a DM, as before.
    /// </summary>
    public async Task DeleteAsync(Guid currentUserId, Guid characterId, CancellationToken cancellationToken = default, Guid? companionId = null)
    {
        var loaded = await tracker.LoadWithRoleAsync(currentUserId, characterId, cancellationToken);
        if (companionId is null && !loaded.IsDm)
        {
            throw AppException.Forbidden("Solo un DM puede quitar el compañero animal.");
        }

        var companion = await FindAsync(loaded.Character.Id, companionId, cancellationToken);
        companions.Remove(companion);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await tracker.NotifyAsync(loaded.Character, cancellationToken);
    }

    private async Task<CharacterCompanion> FindAsync(Guid characterId, Guid? companionId, CancellationToken cancellationToken) =>
        companionId is { } id
            ? (await companions.ListByCharacterAsync(characterId, cancellationToken)).FirstOrDefault(c => c.Id == id) ?? throw CompanionPlanner.NoCompanion()
            : await companions.GetByCharacterAsync(characterId, cancellationToken) ?? throw CompanionPlanner.NoCompanion();
}
