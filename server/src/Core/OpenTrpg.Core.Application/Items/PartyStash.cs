using OpenTrpg.Core.Application.Abstractions;
using OpenTrpg.Core.Application.Abstractions.Persistence;
using OpenTrpg.Core.Application.Campaigns;
using OpenTrpg.Core.Application.Characters;
using OpenTrpg.Core.Application.Common;
using OpenTrpg.Core.Application.Party;
using OpenTrpg.Core.Domain.Campaigns;
using OpenTrpg.Core.Domain.Catalog;
using OpenTrpg.Core.Domain.Characters;
using OpenTrpg.Core.Domain.Items;
using FluentValidation;
using OpenTrpg.Core.Application.Systems;

namespace OpenTrpg.Core.Application.Items;

// Party stash: loot and gold of the campaign that belong to nobody yet. Every member sees it; the DM
// manages it; characters take items from it and give them back (players only when the campaign allows
// it, and always with their own characters). Every movement is recorded as a transaction.

// ---- Requests ------------------------------------------------------------------------------------

/// <summary>Loot added by the DM: catalog item and/or overrides (same rules as adding to an inventory).</summary>
public sealed record AddStashItemRequest
{
    public Guid? TemplateId { get; init; }

    public ItemOverridesDto? Overrides { get; init; }

    public int Quantity { get; init; } = 1;

    public string? Notes { get; init; }
}

public sealed class AddStashItemRequestValidator : AbstractValidator<AddStashItemRequest>
{
    public AddStashItemRequestValidator()
    {
        RuleFor(x => x.Quantity).InclusiveBetween(1, ItemLimits.MaxQuantity)
            .WithMessage($"La cantidad debe estar entre 1 y {ItemLimits.MaxQuantity}.");
        RuleFor(x => x.Overrides)
            .Must((request, overrides) => request.TemplateId is not null || !string.IsNullOrWhiteSpace(overrides?.Name))
            .WithMessage("Indica un objeto del catálogo o el nombre del objeto personalizado.");
        RuleFor(x => x.Overrides!).SetValidator(new ItemOverridesDtoValidator()).When(x => x.Overrides is not null);
        RuleFor(x => x.Notes).MaximumLength(ItemLimits.NotesMaxLength)
            .WithMessage($"Las notas no pueden superar los {ItemLimits.NotesMaxLength} caracteres.");
    }
}

/// <summary>Absent fields do not change; <c>notes: null</c> clears the notes.</summary>
public sealed record UpdateStashItemRequest
{
    public int? Quantity { get; init; }

    public Optional<string?> Notes { get; init; }
}

public sealed class UpdateStashItemRequestValidator : AbstractValidator<UpdateStashItemRequest>
{
    public UpdateStashItemRequestValidator()
    {
        RuleFor(x => x.Quantity).InclusiveBetween(1, ItemLimits.MaxQuantity)
            .WithMessage($"La cantidad debe estar entre 1 y {ItemLimits.MaxQuantity}.");
        RuleFor(x => x.Notes)
            .Must(n => !n.IsSet || n.Value is null || n.Value.Trim().Length <= ItemLimits.NotesMaxLength)
            .WithMessage($"Las notas no pueden superar los {ItemLimits.NotesMaxLength} caracteres.");
    }
}

/// <summary>Units of a stash entry that go to the inventory of a character.</summary>
public sealed record TakeStashItemRequest(Guid CharacterId, int Quantity = 1);

public sealed class TakeStashItemRequestValidator : AbstractValidator<TakeStashItemRequest>
{
    public TakeStashItemRequestValidator()
    {
        RuleFor(x => x.CharacterId).NotEmpty().WithMessage("Indica el personaje.");
        RuleFor(x => x.Quantity).InclusiveBetween(1, ItemLimits.MaxQuantity)
            .WithMessage($"La cantidad debe estar entre 1 y {ItemLimits.MaxQuantity}.");
    }
}

/// <summary>Units of an inventory entry of a character given back to the stash.</summary>
public sealed record ReturnStashItemRequest(Guid CharacterId, Guid CharacterItemId, int Quantity = 1);

public sealed class ReturnStashItemRequestValidator : AbstractValidator<ReturnStashItemRequest>
{
    public ReturnStashItemRequestValidator()
    {
        RuleFor(x => x.CharacterId).NotEmpty().WithMessage("Indica el personaje.");
        RuleFor(x => x.CharacterItemId).NotEmpty().WithMessage("Indica el objeto que quieres devolver.");
        RuleFor(x => x.Quantity).InclusiveBetween(1, ItemLimits.MaxQuantity)
            .WithMessage($"La cantidad debe estar entre 1 y {ItemLimits.MaxQuantity}.");
    }
}

/// <summary>Shared gold added (positive) or withdrawn (negative), in copper pieces.</summary>
public sealed record StashGoldRequest(int DeltaCp);

public sealed class StashGoldRequestValidator : AbstractValidator<StashGoldRequest>
{
    public StashGoldRequestValidator()
    {
        RuleFor(x => x.DeltaCp)
            .NotEqual(0).WithMessage("Indica una cantidad de oro distinta de 0.")
            .InclusiveBetween(-Character.MaxMoney, Character.MaxMoney)
            .WithMessage($"La cantidad debe estar entre -{Character.MaxMoney} y {Character.MaxMoney} pc.");
    }
}

/// <param name="CharacterIds">Characters that receive a share; null or empty = every active character.</param>
public sealed record SplitStashGoldRequest(IReadOnlyList<Guid>? CharacterIds = null);

public sealed class SplitStashGoldRequestValidator : AbstractValidator<SplitStashGoldRequest>
{
    public SplitStashGoldRequestValidator()
    {
        RuleFor(x => x.CharacterIds!)
            .Must(ids => ids.Count <= PartyRestRequestValidator.MaxCharacters)
            .WithMessage($"No se admiten más de {PartyRestRequestValidator.MaxCharacters} personajes.")
            .Must(ids => ids.All(id => id != Guid.Empty)).WithMessage("Indica personajes válidos.")
            .When(x => x.CharacterIds is not null)
            .OverridePropertyName("characterIds");
    }
}

// ---- DTOs ----------------------------------------------------------------------------------------

/// <param name="Charges">Remaining charges of an item given back with charges (null otherwise).</param>
public sealed record PartyStashItemDto(
    Guid Id,
    Guid? TemplateId,
    EffectiveItemDto Item,
    int Quantity,
    int? Charges,
    int? ChargesMax,
    string? Notes,
    DateTimeOffset AddedAt,
    string? AddedByDisplayName);

/// <param name="CopperPieces">Shared gold, in copper pieces.</param>
/// <param name="PlayersCanTakeFromStash">Whether players take items (and give them back) by themselves.</param>
public sealed record PartyStashDto(long CopperPieces, bool PlayersCanTakeFromStash, IReadOnlyList<PartyStashItemDto> Items);

public static class StashErrors
{
    public static AppException ItemNotFound() => AppException.NotFound("El objeto no está en el alijo del grupo.");

    public static AppException PlayersCannotTake() => AppException.Forbidden("El DM no permite a los jugadores tomar ni devolver objetos del alijo.");
}

// ---- Shared --------------------------------------------------------------------------------------

/// <summary>Loads stash entries and characters for the stash use cases, records transactions and builds the DTOs.</summary>
public sealed class StashSupport(
    ICampaignRepository campaigns,
    IPartyStashRepository stash,
    ICharacterRepository characters,
    IItemTemplateRepository templates,
    IUserRepository users,
    ITransactionRepository transactions)
{
    public const string GoldAddedName = "Oro añadido al alijo";
    public const string GoldWithdrawnName = "Oro retirado del alijo";
    public const string GoldShareName = "Reparto del oro del grupo";

    /// <summary>Tracked entry of the campaign's stash (404 when it is in another campaign).</summary>
    public async Task<PartyStashItem> LoadItemAsync(Guid campaignId, Guid itemId, CancellationToken cancellationToken)
    {
        var item = await stash.GetAsync(itemId, cancellationToken);
        return item is not null && item.CampaignId == campaignId ? item : throw StashErrors.ItemNotFound();
    }

    /// <summary>
    /// Tracked character of the campaign that the actor may move items for: their own, or any as DM
    /// (400 when it is not in the campaign, 403 when it belongs to someone else).
    /// </summary>
    public async Task<Character> LoadCharacterAsync(Guid campaignId, Guid characterId, Guid actorUserId, bool actorIsDm, CancellationToken cancellationToken)
    {
        var character = await characters.GetWithDetailsAsync(characterId, cancellationToken);
        if (character is null || character.CampaignId != campaignId)
        {
            throw AppException.Validation("characterId", "El personaje no existe en la campaña.");
        }

        return actorIsDm || character.IsOwnedBy(actorUserId)
            ? character
            : throw AppException.Forbidden("Solo puedes mover objetos con tus propios personajes.");
    }

    /// <summary>Read-only campaign (for its stash settings).</summary>
    public async Task<Campaign> CampaignAsync(Guid campaignId, CancellationToken cancellationToken) =>
        await campaigns.GetByIdAsync(campaignId, cancellationToken) ?? throw CampaignErrors.CampaignNotFound();

    public async Task<ItemTemplate?> TemplateAsync(Guid? templateId, CancellationToken cancellationToken) =>
        templateId is { } id ? (await templates.ListByIdsAsync([id], cancellationToken)).SingleOrDefault() : null;

    /// <summary>
    /// Puts units into the stash: on an entry of the same catalog item when the item stacks (no
    /// overrides, no charges, no notes), otherwise as a new entry.
    /// </summary>
    public async Task<PartyStashItem> PutAsync(
        Guid campaignId,
        Guid? templateId,
        ItemOverrides overrides,
        EffectiveItem effective,
        int quantity,
        string? notes,
        int? charges,
        int? chargesMax,
        Guid actorUserId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var hasCharges = chargesMax is not null;
        if (effective.IsStackable && string.IsNullOrWhiteSpace(notes))
        {
            var existing = (await stash.ListByCampaignAsync(campaignId, cancellationToken))
                .FirstOrDefault(i => i.CanStackWith(templateId, overrides, hasCharges));
            if (existing is not null)
            {
                existing.AddQuantity(quantity);
                return existing;
            }
        }

        var item = PartyStashItem.Create(campaignId, templateId, overrides, quantity, notes, actorUserId, now, charges, chargesMax);
        stash.Add(item);
        return item;
    }

    public void Record(Guid campaignId, Guid? characterId, Guid actorUserId, TransactionType type, string itemName, int quantity, long totalCp, DateTimeOffset now) =>
        transactions.Add(Transaction.Record(campaignId, null, characterId, actorUserId, type, itemName, quantity, totalCp, now));

    public async Task<PartyStashDto> BuildAsync(Guid campaignId, CancellationToken cancellationToken)
    {
        var campaign = await CampaignAsync(campaignId, cancellationToken);
        var items = await stash.ListByCampaignAsync(campaignId, cancellationToken);
        var loaded = await InventoryView.LoadTemplatesAsync(templates, items.Select(i => i.TemplateId), cancellationToken);
        var names = await users.GetDisplayNamesAsync(items.Select(i => i.AddedByUserId).Distinct().ToList(), cancellationToken);
        return new PartyStashDto(
            campaign.StashCopperPieces,
            campaign.PlayersCanTakeFromStash,
            items.Select(i => ToDto(i, InventoryView.TemplateOf(loaded, i.TemplateId), names.GetValueOrDefault(i.AddedByUserId))).ToList());
    }

    public async Task<PartyStashItemDto> BuildItemAsync(PartyStashItem item, CancellationToken cancellationToken)
    {
        var template = await TemplateAsync(item.TemplateId, cancellationToken);
        var names = await users.GetDisplayNamesAsync([item.AddedByUserId], cancellationToken);
        return ToDto(item, template, names.GetValueOrDefault(item.AddedByUserId));
    }

    private static PartyStashItemDto ToDto(PartyStashItem item, ItemTemplate? template, string? addedBy) => new(
        item.Id,
        item.TemplateId,
        EffectiveItemDto.From(EffectiveItem.Resolve(template, item.Overrides)),
        item.Quantity,
        item.Charges,
        item.ChargesMax,
        item.Notes,
        item.AddedAt,
        addedBy);
}

// ---- Handlers ------------------------------------------------------------------------------------

/// <summary>The party stash: shared gold and items (every member).</summary>
public sealed class GetStashHandler(ICampaignAccess access, StashSupport support)
{
    public async Task<PartyStashDto> HandleAsync(Guid currentUserId, Guid campaignId, CancellationToken cancellationToken = default)
    {
        await access.RequireAsync(campaignId, currentUserId, CampaignRole.Player, cancellationToken);
        return await support.BuildAsync(campaignId, cancellationToken);
    }
}

/// <summary>A DM adds loot to the stash (StashAdd).</summary>
public sealed class AddStashItemHandler(
    ICampaignAccess access,
    StashSupport support,
    InventoryOperations operations,
    IUnitOfWork unitOfWork,
    ICampaignNotifier notifier,
    IDateTimeProvider clock)
{
    public async Task<PartyStashItemDto> HandleAsync(Guid currentUserId, Guid campaignId, AddStashItemRequest request, CancellationToken cancellationToken = default)
    {
        await access.RequireAsync(campaignId, currentUserId, CampaignRole.DM, cancellationToken);
        var (template, overrides) = await operations.ResolveAddAsync(
            campaignId,
            new AddInventoryItemRequest { TemplateId = request.TemplateId, Overrides = request.Overrides, Quantity = request.Quantity },
            cancellationToken);
        var effective = EffectiveItem.Resolve(template, overrides);
        var now = clock.UtcNow;

        var item = await support.PutAsync(campaignId, template?.Id, overrides, effective, request.Quantity, request.Notes, null, null, currentUserId, now, cancellationToken);
        support.Record(campaignId, null, currentUserId, TransactionType.StashAdd, effective.Name, request.Quantity, 0, now);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await notifier.StashUpdatedAsync(campaignId, now, cancellationToken);
        return await support.BuildItemAsync(item, cancellationToken);
    }
}

/// <summary>A DM changes the quantity or the notes of a stash entry.</summary>
public sealed class UpdateStashItemHandler(
    ICampaignAccess access,
    StashSupport support,
    IUnitOfWork unitOfWork,
    ICampaignNotifier notifier,
    IDateTimeProvider clock)
{
    public async Task<PartyStashItemDto> HandleAsync(Guid currentUserId, Guid campaignId, Guid itemId, UpdateStashItemRequest request, CancellationToken cancellationToken = default)
    {
        await access.RequireAsync(campaignId, currentUserId, CampaignRole.DM, cancellationToken);
        var item = await support.LoadItemAsync(campaignId, itemId, cancellationToken);
        item.Update(request.Quantity, request.Notes.IsSet, request.Notes.Value);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await notifier.StashUpdatedAsync(campaignId, clock.UtcNow, cancellationToken);
        return await support.BuildItemAsync(item, cancellationToken);
    }
}

/// <summary>A DM removes a stash entry (StashRemove).</summary>
public sealed class DeleteStashItemHandler(
    ICampaignAccess access,
    StashSupport support,
    IPartyStashRepository stash,
    IUnitOfWork unitOfWork,
    ICampaignNotifier notifier,
    IDateTimeProvider clock)
{
    public async Task HandleAsync(Guid currentUserId, Guid campaignId, Guid itemId, CancellationToken cancellationToken = default)
    {
        await access.RequireAsync(campaignId, currentUserId, CampaignRole.DM, cancellationToken);
        var item = await support.LoadItemAsync(campaignId, itemId, cancellationToken);
        var effective = EffectiveItem.Resolve(await support.TemplateAsync(item.TemplateId, cancellationToken), item.Overrides);
        var now = clock.UtcNow;

        stash.Remove(item);
        support.Record(campaignId, null, currentUserId, TransactionType.StashRemove, effective.Name, item.Quantity, 0, now);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await notifier.StashUpdatedAsync(campaignId, now, cancellationToken);
    }
}

/// <summary>
/// Units of a stash entry go to the inventory of a character (StashTake): the owner of the character
/// when the campaign allows it, or a DM on behalf of any character. Atomic: one <c>SaveChanges</c>, and
/// the versions of the entry and the character make a concurrent take of the same units fail with 409.
/// </summary>
public sealed class TakeStashItemHandler(
    ICampaignAccess access,
    StashSupport support,
    IPartyStashRepository stash,
    IUnitOfWork unitOfWork,
    ICampaignNotifier notifier,
    IDateTimeProvider clock)
{
    public async Task<PartyStashDto> HandleAsync(Guid currentUserId, Guid campaignId, Guid itemId, TakeStashItemRequest request, CancellationToken cancellationToken = default)
    {
        var role = await access.RequireAsync(campaignId, currentUserId, CampaignRole.Player, cancellationToken);
        var isDm = role.IsAtLeast(CampaignRole.DM);
        if (!isDm && !(await support.CampaignAsync(campaignId, cancellationToken)).PlayersCanTakeFromStash)
        {
            throw StashErrors.PlayersCannotTake();
        }

        var item = await support.LoadItemAsync(campaignId, itemId, cancellationToken);
        var character = await support.LoadCharacterAsync(campaignId, request.CharacterId, currentUserId, isDm, cancellationToken);
        if (request.Quantity > item.Quantity)
        {
            throw AppException.Validation("quantity", $"Solo hay {item.Quantity} unidades de este objeto en el alijo.");
        }

        var effective = EffectiveItem.Resolve(await support.TemplateAsync(item.TemplateId, cancellationToken), item.Overrides);
        var now = clock.UtcNow;
        character.ReceiveItem(item.TemplateId, item.Overrides.Copy(), request.Quantity, effective, item.Charges, item.ChargesMax, now);
        item.RemoveQuantity(request.Quantity);
        if (item.Quantity == 0)
        {
            stash.Remove(item);
        }

        support.Record(campaignId, character.Id, currentUserId, TransactionType.StashTake, effective.Name, request.Quantity, 0, now);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await notifier.StashUpdatedAsync(campaignId, now, cancellationToken);
        await notifier.CharacterUpdatedAsync(campaignId, character.Id, now, cancellationToken);
        return await support.BuildAsync(campaignId, cancellationToken);
    }
}

/// <summary>
/// Units of an inventory entry go back to the stash (StashReturn), stacking on an entry of the same
/// catalog item when possible. Same permissions as <see cref="TakeStashItemHandler"/>; attuned items
/// cannot be given back. Returning an equipped item recalculates the sheet.
/// </summary>
public sealed class ReturnStashItemHandler(
    ICampaignAccess access,
    StashSupport support,
    InventoryReader inventory,
    InventoryHooks hooks,
    IUnitOfWork unitOfWork,
    ICampaignNotifier notifier,
    IDateTimeProvider clock)
{
    public async Task<PartyStashDto> HandleAsync(Guid currentUserId, Guid campaignId, ReturnStashItemRequest request, CancellationToken cancellationToken = default)
    {
        var role = await access.RequireAsync(campaignId, currentUserId, CampaignRole.Player, cancellationToken);
        var isDm = role.IsAtLeast(CampaignRole.DM);
        if (!isDm && !(await support.CampaignAsync(campaignId, cancellationToken)).PlayersCanTakeFromStash)
        {
            throw StashErrors.PlayersCannotTake();
        }

        var character = await support.LoadCharacterAsync(campaignId, request.CharacterId, currentUserId, isDm, cancellationToken);
        var entry = character.FindItem(request.CharacterItemId);
        if (entry.Attuned)
        {
            throw AppException.Validation("characterItemId", "Termina la sintonización antes de devolver el objeto.");
        }

        if (request.Quantity > entry.Quantity)
        {
            throw AppException.Validation("quantity", $"Solo hay {entry.Quantity} unidades de este objeto.");
        }

        var templates = await inventory.TemplatesAsync(character, cancellationToken);
        var effective = InventoryView.Resolve(templates, entry);
        var overrides = entry.Overrides.Copy();
        var (templateId, charges, chargesMax, wasEquipped) = (entry.TemplateId, entry.Charges, entry.ChargesMax, entry.Equipped);
        var now = clock.UtcNow;

        character.RemoveItem(entry.Id, request.Quantity, now);
        if (wasEquipped)
        {
            // Giving back an equipped item can lower the sheet (item modifiers): cap the current hit points.
            await hooks.ChangedAsync(character, InventoryChangeKinds.Returned, entry.Id, cancellationToken);
        }

        await support.PutAsync(campaignId, templateId, overrides, effective, request.Quantity, null, charges, chargesMax, currentUserId, now, cancellationToken);
        support.Record(campaignId, character.Id, currentUserId, TransactionType.StashReturn, effective.Name, request.Quantity, 0, now);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await notifier.StashUpdatedAsync(campaignId, now, cancellationToken);
        await notifier.CharacterUpdatedAsync(campaignId, character.Id, now, cancellationToken);
        return await support.BuildAsync(campaignId, cancellationToken);
    }
}

/// <summary>A DM adds shared gold to the stash or withdraws it (never below 0) (StashGoldAdd).</summary>
public sealed class StashGoldHandler(
    ICampaignAccess access,
    ICampaignRepository campaigns,
    StashSupport support,
    IUnitOfWork unitOfWork,
    ICampaignNotifier notifier,
    IDateTimeProvider clock)
{
    public async Task<PartyStashDto> HandleAsync(Guid currentUserId, Guid campaignId, StashGoldRequest request, CancellationToken cancellationToken = default)
    {
        await access.RequireAsync(campaignId, currentUserId, CampaignRole.DM, cancellationToken);
        var campaign = await campaigns.GetWithMembersAsync(campaignId, cancellationToken) ?? throw CampaignErrors.CampaignNotFound();
        var now = clock.UtcNow;

        campaign.AdjustStashGold(request.DeltaCp, now);
        support.Record(
            campaignId,
            null,
            currentUserId,
            TransactionType.StashGoldAdd,
            request.DeltaCp > 0 ? StashSupport.GoldAddedName : StashSupport.GoldWithdrawnName,
            1,
            Math.Abs((long)request.DeltaCp),
            now);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await notifier.StashUpdatedAsync(campaignId, now, cancellationToken);
        return await support.BuildAsync(campaignId, cancellationToken);
    }
}

/// <summary>
/// A DM splits the shared gold in equal shares of copper pieces among the active characters (or the
/// given ones); the remainder stays in the stash. One StashGoldSplit transaction per character.
/// </summary>
public sealed class SplitStashGoldHandler(
    PartyLoader party,
    ICampaignRepository campaigns,
    StashSupport support,
    IUnitOfWork unitOfWork,
    ICampaignNotifier notifier,
    IDateTimeProvider clock)
{
    public async Task<PartyStashDto> HandleAsync(Guid currentUserId, Guid campaignId, SplitStashGoldRequest request, CancellationToken cancellationToken = default)
    {
        var targets = PartyLoader.Select(await party.LoadAsync(campaignId, currentUserId, cancellationToken), request.CharacterIds);
        var campaign = await campaigns.GetWithMembersAsync(campaignId, cancellationToken) ?? throw CampaignErrors.CampaignNotFound();
        var now = clock.UtcNow;

        var share = campaign.SplitStashGold(targets.Count, now);
        foreach (var character in targets)
        {
            character.AdjustMoney(share, now);
            support.Record(campaignId, character.Id, currentUserId, TransactionType.StashGoldSplit, StashSupport.GoldShareName, 1, share, now);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await notifier.StashUpdatedAsync(campaignId, now, cancellationToken);
        foreach (var character in targets)
        {
            await notifier.CharacterUpdatedAsync(campaignId, character.Id, now, cancellationToken);
        }

        return await support.BuildAsync(campaignId, cancellationToken);
    }
}
