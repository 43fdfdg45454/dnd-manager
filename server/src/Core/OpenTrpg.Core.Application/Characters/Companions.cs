using System.Text.Json;
using OpenTrpg.Core.Application.Abstractions;
using OpenTrpg.Core.Application.Abstractions.Persistence;
using OpenTrpg.Core.Application.Catalog;
using OpenTrpg.Core.Application.ChangeRequests;
using OpenTrpg.Core.Application.Common;
using OpenTrpg.Core.Domain.Catalog;
using OpenTrpg.Core.Domain.Characters;
using FluentValidation;

namespace OpenTrpg.Core.Application.Characters;

// Animal companion (phase 25, block 6): a beast of the catalog chosen through a subclass feature with a companion rule.

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
public sealed class CompanionPlanner(ICatalogRepository catalog, IBeastCatalog beasts, ICharacterCompanionRepository companions)
{
    /// <summary>Rule used for a companion whose feature the character no longer has: the beast as it is.</summary>
    private static readonly CompanionRule NoFeature = new(CompanionRule.MaxChallengeRatingLimit, [], null, false, false);

    public static AppException NoCompanionFeature() =>
        AppException.Conflict("El personaje no tiene un rasgo de compañero animal a su nivel.");

    public static AppException NoCompanion() => AppException.NotFound("El personaje no tiene compañero animal.");

    /// <summary>The companion feature the character has reached, or null.</summary>
    public async Task<CompanionGrant?> GrantAsync(Dnd5eCharacter character, CancellationToken cancellationToken)
    {
        var subclasses = character.Classes.Select(c => c.SubclassIndex).OfType<string>().Distinct(StringComparer.Ordinal).ToList();
        var features = await catalog.ListSubclassFeatureResourcesAsync(subclasses, cancellationToken);
        return CompanionGrants.Find(character, features);
    }

    /// <summary>The beast, if the catalog has it and the rule allows it; a validation error on <c>beastIndex</c> otherwise.</summary>
    public BeastDto RequireBeast(CompanionRule rule, string beastIndex)
    {
        var index = beastIndex.Trim();
        var beast = Beast(index) ?? throw AppException.Validation("beastIndex", $"La bestia '{index}' no existe en el catálogo.");
        if (!rule.Allows(beast.ChallengeRating, beast.Size))
        {
            throw AppException.Validation(
                "beastIndex",
                $"{beast.Name} no puede ser tu compañero: {DescribeFilter(rule)}.");
        }

        return beast;
    }

    /// <summary>Maximum hit points of a companion of that beast (they do not depend on the proficiency bonus).</summary>
    public static int MaxHitPoints(BeastDto beast, CompanionGrant? grant, int? maxOverride = null) =>
        CompanionCalculator.Calculate(ToDomain(beast), grant?.Rule ?? NoFeature, grant?.ClassIndex ?? string.Empty, grant?.ClassLevel ?? 0, 0, maxOverride)
            .HitPointsMax.Total;

    /// <summary>Maximum hit points of an existing companion (its stored hit points when the beast is gone from the catalog).</summary>
    public int MaxHitPoints(CharacterCompanion companion, CompanionGrant? grant) =>
        Beast(companion.BeastIndex) is { } beast
            ? MaxHitPoints(beast, grant, companion.HitPointsMaxOverride)
            : companion.HitPointsMaxOverride ?? companion.HitPointsCurrent;

    /// <summary>
    /// Creates the companion, renames it or changes its beast (back to full hit points). The caller has checked who may do it;
    /// the beast is checked here against the feature the character has reached.
    /// </summary>
    public async Task<CharacterCompanion> ApplyAsync(Dnd5eCharacter character, string beastIndex, string name, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var grant = await GrantAsync(character, cancellationToken) ?? throw NoCompanionFeature();
        var beast = RequireBeast(grant.Rule, beastIndex);
        var existing = await companions.GetByCharacterAsync(character.Id, cancellationToken);
        if (existing is null)
        {
            var created = CharacterCompanion.Create(character.Id, beast.Index, name, MaxHitPoints(beast, grant), now);
            companions.Add(created);
            return created;
        }

        if (string.Equals(existing.BeastIndex, beast.Index, StringComparison.Ordinal))
        {
            existing.Rename(name, now);
        }
        else
        {
            existing.ChangeBeast(beast.Index, name, MaxHitPoints(beast, grant, existing.HitPointsMaxOverride), now);
        }

        return existing;
    }

    /// <summary>Applies an approved <see cref="Dnd5eChangeRequestTypes.Companion"/> request.</summary>
    public Task ApplyApprovedAsync(Dnd5eCharacter character, string payloadJson, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var payload = CompanionPayload.Parse(payloadJson)
            ?? throw AppException.Validation("payload", "El contenido de la solicitud no es un compañero válido.");
        return ApplyAsync(character, payload.BeastIndex, payload.Name, now, cancellationToken);
    }

    /// <summary>A long rest brings the companions of the given characters back to full hit points.</summary>
    public async Task RestoreAfterLongRestAsync(IReadOnlyList<Dnd5eCharacter> characters, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var list = await companions.ListByCharactersAsync(characters.Select(c => c.Id).ToList(), cancellationToken);
        foreach (var companion in list)
        {
            var character = characters.First(c => c.Id == companion.CharacterId);
            companion.RestoreHitPoints(MaxHitPoints(companion, await GrantAsync(character, cancellationToken)), now);
        }
    }

    /// <summary>The feature as the detail shows it.</summary>
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
    public CharacterCompanionDto BuildDto(CharacterCompanion companion, CompanionGrant? grant, int proficiencyBonus)
    {
        var found = Beast(companion.BeastIndex);
        var beast = found ?? new BeastDto(
            companion.BeastIndex, companion.BeastIndex, string.Empty, string.Empty, 0, "0", 0, 2, 10, null,
            Math.Max(1, companion.HitPointsCurrent), string.Empty, null,
            new Dictionary<string, int>(), new Dictionary<string, int>(), new Dictionary<string, int>(), new Dictionary<string, int>(),
            new Dictionary<string, string>(), 10, string.Empty, [], [], [], [], [], [], null);
        var rule = grant?.Rule ?? NoFeature;
        var block = CompanionCalculator.Calculate(
            ToDomain(beast), rule, grant?.ClassIndex ?? string.Empty, grant?.ClassLevel ?? 0, proficiencyBonus, companion.HitPointsMaxOverride);

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
            found is null);
    }

    /// <summary>The beast of the catalog with that index, or null.</summary>
    public BeastDto? Beast(string index) =>
        beasts.All.FirstOrDefault(b => string.Equals(b.Index, index, StringComparison.OrdinalIgnoreCase));

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
            .ToList());
}

/// <summary>Payload (and snapshot) of a <see cref="Dnd5eChangeRequestTypes.Companion"/> request.</summary>
public sealed record CompanionPayload(string BeastIndex, string BeastName, string Name)
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
    public async Task<CompanionResult> HandleAsync(Guid currentUserId, Guid characterId, SetCompanionRequest request, CancellationToken cancellationToken = default)
    {
        var loaded = await loader.LoadAsync(characterId, currentUserId, cancellationToken);
        var character = loaded.Character;
        character.EnsureCanTrack(currentUserId, loaded.IsDm);
        var grant = await planner.GrantAsync(character, cancellationToken) ?? throw CompanionPlanner.NoCompanionFeature();
        var beast = planner.RequireBeast(grant.Rule, request.BeastIndex);
        var name = CharacterCompanion.NormalizeName(request.Name);
        var existing = await companions.GetByCharacterAsync(character.Id, cancellationToken);
        var now = clock.UtcNow;

        var direct = existing is null
            || string.Equals(existing.BeastIndex, beast.Index, StringComparison.Ordinal)
            || character.ResolveSheetEdit(currentUserId, loaded.IsDm) == SheetEditMode.Direct;
        if (direct)
        {
            await planner.ApplyAsync(character, beast.Index, name, now, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await notifier.CharacterUpdatedAsync(character.CampaignId, character.Id, now, cancellationToken);
            return new CompanionResult(await sheets.BuildDetailAsync(character, cancellationToken), null);
        }

        var before = new CompanionPayload(existing!.BeastIndex, planner.Beast(existing.BeastIndex)?.Name ?? existing.BeastIndex, existing.Name);
        var changeRequest = ChangeRequest.Create(
            character.CampaignId,
            character.Id,
            currentUserId,
            Dnd5eChangeRequestTypes.Companion,
            new CompanionPayload(beast.Index, beast.Name, name).ToJson(),
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
    public async Task<CharacterDetailDto> TrackHitPointsAsync(Guid currentUserId, Guid characterId, CompanionHpRequest request, CancellationToken cancellationToken = default)
    {
        var character = await tracker.LoadAsync(currentUserId, characterId, cancellationToken);
        var companion = await companions.GetByCharacterAsync(character.Id, cancellationToken) ?? throw CompanionPlanner.NoCompanion();
        var max = planner.MaxHitPoints(companion, await planner.GrantAsync(character, cancellationToken));
        companion.TrackHitPoints(request.Delta, request.Current, max, clock.UtcNow);
        return await tracker.SaveAsync(character, cancellationToken);
    }

    public async Task DeleteAsync(Guid currentUserId, Guid characterId, CancellationToken cancellationToken = default)
    {
        var loaded = await tracker.LoadWithRoleAsync(currentUserId, characterId, cancellationToken);
        if (!loaded.IsDm)
        {
            throw AppException.Forbidden("Solo un DM puede quitar el compañero animal.");
        }

        var companion = await companions.GetByCharacterAsync(loaded.Character.Id, cancellationToken) ?? throw CompanionPlanner.NoCompanion();
        companions.Remove(companion);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await tracker.NotifyAsync(loaded.Character, cancellationToken);
    }
}
