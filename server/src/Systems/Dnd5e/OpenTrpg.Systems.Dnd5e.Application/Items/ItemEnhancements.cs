using System.Text.Json;
using FluentValidation;
using OpenTrpg.Core.Application;
using OpenTrpg.Core.Application.Abstractions;
using OpenTrpg.Core.Application.Abstractions.Persistence;
using OpenTrpg.Core.Application.ChangeRequests;
using OpenTrpg.Core.Application.Characters;
using OpenTrpg.Core.Application.Common;
using OpenTrpg.Core.Application.Items;
using OpenTrpg.Core.Domain.Catalog;
using OpenTrpg.Core.Domain.Characters;
using OpenTrpg.Core.Domain.Items;
using OpenTrpg.Systems.Dnd5e.Application.Abstractions.Persistence;
using OpenTrpg.Systems.Dnd5e.Application.Characters;
using OpenTrpg.Systems.Dnd5e.Domain.Catalog;
using OpenTrpg.Systems.Dnd5e.Domain.Characters;

namespace OpenTrpg.Systems.Dnd5e.Application.Items;

/// <summary>From stored option indexes to the enhancements the domain applies (phase 35B3, H31).</summary>
public static class ItemEnhancementEffects
{
    /// <summary>The options among <paramref name="indexes"/> that are item enhancements, in order.</summary>
    public static IReadOnlyList<AppliedEnhancement> Applied(IEnumerable<string> indexes, Func<string, OptionDefinition?> option) =>
        indexes
            .Select(option)
            .OfType<OptionDefinition>()
            .Where(o => o.ItemEffects is not null)
            .Select(o => new AppliedEnhancement(o.Index, o.Name, o.ItemEffects!, ItemModifiersOf(o)))
            .ToList();

    /// <summary>The option's constant modifiers of item kinds (the ones an item can carry).</summary>
    public static IReadOnlyList<ItemModifier> ItemModifiersOf(OptionDefinition option) =>
        option.Modifiers
            .Where(m => ItemModifier.IsItemKind(m.Kind) && m.Formula is null && m.Condition is null)
            .Select(m => new ItemModifier(m.Kind, m.Target, m.Value))
            .ToList();

    /// <summary>The enhancements of each inventory entry of <paramref name="character"/>.</summary>
    public static Func<CharacterItem, IReadOnlyList<AppliedEnhancement>> For(Dnd5eCharacter character, Func<string, OptionDefinition?> option)
    {
        var stored = character.ItemEnhancements;
        return item => stored.TryGetValue(item.Id, out var indexes) ? Applied(indexes, option) : [];
    }
}

// ---- DTOs ---------------------------------------------------------------------------------------------------------

/// <summary>What an enhancement option does to an item.</summary>
public sealed record ItemEffectsDto(
    IReadOnlyList<string> AppliesTo,
    int AttackBonus,
    int DamageBonus,
    int ArmorClassBonus,
    IReadOnlyList<string> AddProperties,
    int DamageStep,
    string? DamageType)
{
    public static ItemEffectsDto From(ItemEffects effects) => new(
        effects.AppliesTo, effects.AttackBonus, effects.DamageBonus, effects.ArmorClassBonus, effects.AddProperties, effects.DamageStep, effects.DamageType);
}

/// <summary>An option the feature can apply, with its prerequisites (<see cref="MinLevel"/> of the feature's class, options the item needs first).</summary>
public sealed record ItemEnhancementOptionDto(
    string Index,
    string Name,
    IReadOnlyList<string> Description,
    int? MinLevel,
    IReadOnlyList<string> RequiresOptions,
    bool Available,
    ItemEffectsDto Effects);

/// <summary>The item enhancement feature the character has reached: how many options per item and which ones.</summary>
public sealed record ItemEnhancementFeatureDto(
    string FeatureIndex,
    string FeatureName,
    string ClassIndex,
    int ClassLevel,
    int MaxPerItem,
    IReadOnlyList<ItemEnhancementOptionDto> Options);

/// <summary>An option applied to an item.</summary>
public sealed record AppliedEnhancementDto(string Index, string Name);

/// <summary>
/// An enhanced inventory entry as the sheet shows it: the options, the item's fields after them (damage, type, properties)
/// and the breakdowns of what they add ("attack", "damage", "armorClass").
/// </summary>
public sealed record CharacterItemEnhancementDto(
    Guid ItemId,
    string ItemName,
    IReadOnlyList<AppliedEnhancementDto> Options,
    string? DamageDice,
    string? DamageType,
    IReadOnlyList<string> Properties,
    IReadOnlyDictionary<string, ValueBreakdownDto> Breakdowns)
{
    public static CharacterItemEnhancementDto From(Guid itemId, EnhancedItem enhanced)
    {
        var breakdowns = new Dictionary<string, ValueBreakdownDto>(StringComparer.Ordinal);
        foreach (var (key, kind) in new[] { ("attack", ItemModifierKind.AttackBonus), ("damage", ItemModifierKind.DamageBonus), ("armorClass", ItemModifierKind.ArmorClassBonus) })
        {
            var parts = enhanced.Bonuses.Where(b => b.Modifier.Kind == kind && b.Modifier.Target is null)
                .Select(b => new BreakdownPartDto(BreakdownSources.Item, b.Label, b.Modifier.Value))
                .ToList();
            if (parts.Count > 0)
            {
                breakdowns[key] = new ValueBreakdownDto(parts.Sum(p => p.Value), parts);
            }
        }

        return new CharacterItemEnhancementDto(
            itemId,
            enhanced.Item.Name,
            enhanced.Enhancements.Select(e => new AppliedEnhancementDto(e.Index, e.Name)).ToList(),
            enhanced.Item.DamageDice,
            enhanced.Item.DamageType,
            enhanced.Item.Properties,
            breakdowns);
    }
}

/// <summary>An equipped or carried graft (<c>items[].graft</c>) with its body slot.</summary>
public sealed record CharacterGraftDto(Guid ItemId, string Name, string Slot, bool Equipped);

// ---- Request and handler ---------------------------------------------------------------------------------------------

/// <summary><c>PUT …/items/{itemId}/enhancements</c>: the option indexes the item carries (empty removes them).</summary>
public sealed record SetItemEnhancementsRequest(IReadOnlyList<string>? Options);

public sealed class SetItemEnhancementsRequestValidator : AbstractValidator<SetItemEnhancementsRequest>
{
    public const int MaxOptions = ItemEnhancementRule.MaxPerItemLimit;

    public SetItemEnhancementsRequestValidator()
    {
        RuleFor(x => x.Options).NotNull().WithMessage("Indica las opciones (una lista, vacía para quitarlas).");
        RuleFor(x => x.Options!.Count).LessThanOrEqualTo(MaxOptions).When(x => x.Options is not null).OverridePropertyName("options")
            .WithMessage($"Como máximo {MaxOptions} opciones por objeto.");
    }
}

public sealed record ItemEnhancementResult(CharacterDetailDto? Character, ChangeRequestDto? ChangeRequest);

/// <summary>Payload of an <see cref="Dnd5eChangeRequestTypes.ItemEnhancement"/> request.</summary>
public sealed record ItemEnhancementPayload(Guid ItemId, string ItemName, IReadOnlyList<string> Options)
{
    private static readonly JsonSerializerOptions Options_ = new(JsonSerializerDefaults.Web);

    public string ToJson() => JsonSerializer.Serialize(this, Options_);

    public static ItemEnhancementPayload? Parse(string json)
    {
        try
        {
            var payload = JsonSerializer.Deserialize<ItemEnhancementPayload>(json, Options_);
            return payload is { ItemId: var id, Options: not null } && id != Guid.Empty ? payload : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

/// <summary>
/// The rules of item enhancements: which options an inventory entry may carry and who may apply them. A player whose
/// character has the enhancement feature applies the options of its sets directly (an operation tied to the feature, within
/// the options per item of its level and their prerequisites); without the feature, the change goes to the DM as a
/// <see cref="Dnd5eChangeRequestTypes.ItemEnhancement"/> request. DMs (and owners of drafts) apply directly.
/// </summary>
public sealed class ItemEnhancementPlanner(ICatalogRepository catalog, IItemTemplateRepository itemTemplates)
{
    /// <summary>The feature the character has reached, or null.</summary>
    public async Task<ItemEnhancementGrant?> GrantAsync(Dnd5eCharacter character, CancellationToken cancellationToken)
    {
        var classes = character.Classes.Select(c => c.ClassIndex).Distinct(StringComparer.Ordinal).ToList();
        var subclasses = character.Classes.Select(c => c.SubclassIndex).OfType<string>().Distinct(StringComparer.Ordinal).ToList();
        return ItemEnhancementGrants.Find(character, await catalog.ListFeatureEffectsAsync(classes, subclasses, cancellationToken));
    }

    /// <summary>The feature as the detail shows it, with the options of its sets.</summary>
    public async Task<ItemEnhancementFeatureDto> FeatureDtoAsync(ItemEnhancementGrant grant, CancellationToken cancellationToken)
    {
        var options = await catalog.ListOptionsBySetAsync(grant.Rule.OptionSets, cancellationToken);
        return new ItemEnhancementFeatureDto(
            grant.Feature.Index,
            grant.Feature.Name,
            grant.Feature.ClassIndex,
            grant.ClassLevel,
            grant.MaxPerItem,
            options
                .Where(o => o.ItemEffects is not null)
                .Select(o => new ItemEnhancementOptionDto(
                    o.Index,
                    o.Name,
                    o.Description,
                    o.Prerequisites.MinLevel,
                    o.Prerequisites.Options,
                    (o.Prerequisites.MinLevel ?? 0) <= grant.ClassLevel,
                    ItemEffectsDto.From(o.ItemEffects!)))
                .ToList());
    }

    /// <summary>
    /// Checks <paramref name="indexes"/> for the inventory entry: options that exist in the campaign's catalog, are item
    /// enhancements and apply to that kind of item; with <paramref name="grant"/>, also that they belong to its sets, are
    /// available at its level, have the options they require on the same item and fit in its options per item.
    /// </summary>
    public async Task<(IReadOnlyList<string> Options, string ItemName)> ValidateAsync(
        Dnd5eCharacter character,
        CharacterItem item,
        IReadOnlyList<string> indexes,
        ItemEnhancementGrant? grant,
        CancellationToken cancellationToken)
    {
        var cleaned = indexes.Select(i => i.Trim()).Where(i => i.Length > 0).Distinct(StringComparer.Ordinal).ToList();
        var templates = await InventoryView.LoadTemplatesAsync(itemTemplates, [item.TemplateId], cancellationToken);
        var effective = InventoryView.Resolve(templates, item);
        if (cleaned.Count > 0 && ItemEffects.KindOf(effective) is null)
        {
            throw AppException.Validation("options", $"{effective.Name} no se puede mejorar: solo armas, armaduras y escudos.");
        }

        var definitions = (await catalog.ListOptionsByIndexAsync(cleaned, cancellationToken)).ToDictionary(o => o.Index, StringComparer.Ordinal);
        for (var i = 0; i < cleaned.Count; i++)
        {
            var field = $"options[{i}]";
            if (!definitions.TryGetValue(cleaned[i], out var option) || option.ItemEffects is not { } effects)
            {
                throw AppException.Validation(field, $"La mejora '{cleaned[i]}' no existe en el catálogo de la campaña.");
            }

            if (!effects.AppliesToItem(effective))
            {
                throw AppException.Validation(field, $"{option.Name} no se aplica a {effective.Name}.");
            }

            if (grant is null)
            {
                continue;
            }

            if (!grant.Rule.OptionSets.Contains(option.SetId, StringComparer.Ordinal))
            {
                throw AppException.Validation(field, $"{option.Name} no es una mejora de {grant.Feature.Name}.");
            }

            if (option.Prerequisites.MinLevel is { } minLevel && minLevel > grant.ClassLevel)
            {
                throw AppException.Validation(field, $"{option.Name} requiere nivel {minLevel} de {grant.Feature.Name}.");
            }

            if (option.Prerequisites.Options.FirstOrDefault(required => !cleaned.Contains(required, StringComparer.Ordinal)) is { } missing)
            {
                throw AppException.Validation(field, $"{option.Name} requiere que el objeto tenga antes la mejora '{missing}'.");
            }
        }

        if (grant is not null && cleaned.Count > grant.MaxPerItem)
        {
            throw AppException.Validation("options", $"A tu nivel, {grant.Feature.Name} permite {grant.MaxPerItem} mejoras por objeto.");
        }

        return (cleaned, effective.Name);
    }
}

/// <summary><c>PUT /characters/{id}/items/{itemId}/enhancements</c>: the owner or a DM (see <see cref="ItemEnhancementPlanner"/>).</summary>
public sealed class SetItemEnhancementsHandler(
    Dnd5eCharacterLoader loader,
    ItemEnhancementPlanner planner,
    ICharacterSheetService sheets,
    IChangeRequestRepository changeRequests,
    IUnitOfWork unitOfWork,
    ICampaignNotifier notifier,
    IDateTimeProvider clock)
{
    public async Task<ItemEnhancementResult> HandleAsync(
        Guid currentUserId,
        Guid characterId,
        Guid itemId,
        SetItemEnhancementsRequest request,
        CancellationToken cancellationToken = default)
    {
        var loaded = await loader.LoadAsync(characterId, currentUserId, cancellationToken);
        var character = loaded.Character;
        character.EnsureCanTrack(currentUserId, loaded.IsDm);
        var item = character.Items.FirstOrDefault(i => i.Id == itemId)
            ?? throw AppException.NotFound("El objeto no está en el inventario del personaje.");
        var grant = await planner.GrantAsync(character, cancellationToken);
        var byFeature = grant is not null && !loaded.IsDm;
        var (options, name) = await planner.ValidateAsync(character, item, request.Options ?? [], byFeature ? grant : null, cancellationToken);
        var now = clock.UtcNow;

        if (byFeature || character.ResolveSheetEdit(currentUserId, loaded.IsDm) == SheetEditMode.Direct)
        {
            character.SetItemEnhancements(item.Id, options, now);
            await sheets.RecalculateAsync(character, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await notifier.CharacterUpdatedAsync(character.CampaignId, character.Id, now, cancellationToken);
            return new ItemEnhancementResult(await sheets.BuildDetailAsync(character, cancellationToken), null);
        }

        var changeRequest = ChangeRequest.Create(
            character.CampaignId,
            character.Id,
            currentUserId,
            Dnd5eChangeRequestTypes.ItemEnhancement,
            new ItemEnhancementPayload(item.Id, name, options).ToJson(),
            now,
            new ItemEnhancementPayload(item.Id, name, character.ItemEnhancementsOf(item.Id)).ToJson());
        changeRequests.Add(changeRequest);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await notifier.ChangeRequestUpdatedAsync(character.CampaignId, character.Id, changeRequest.Id, now, cancellationToken);

        var view = (await changeRequests.ListViewsAsync(new ChangeRequestQuery(Id: changeRequest.Id), cancellationToken)).Single();
        return new ItemEnhancementResult(null, ChangeRequestDto.From(view));
    }

    /// <summary>Applies an approved <see cref="Dnd5eChangeRequestTypes.ItemEnhancement"/> request (the options are checked again, without the feature's limits).</summary>
    public async Task ApplyApprovedAsync(Dnd5eCharacter character, string payloadJson, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var payload = ItemEnhancementPayload.Parse(payloadJson)
            ?? throw AppException.Validation("payload", "El contenido de la solicitud no es una mejora de objeto válida.");
        var item = character.Items.FirstOrDefault(i => i.Id == payload.ItemId)
            ?? throw AppException.Conflict("El objeto ya no está en el inventario del personaje.");
        var (options, _) = await planner.ValidateAsync(character, item, payload.Options, null, cancellationToken);
        character.SetItemEnhancements(item.Id, options, now);
        await sheets.RecalculateAsync(character, cancellationToken);
    }
}
