using System.Text.Json;
using System.Text.Json.Serialization;
using OpenTrpg.Core.Application.Abstractions;
using OpenTrpg.Core.Application.Abstractions.Persistence;
using OpenTrpg.Core.Application.ChangeRequests;
using OpenTrpg.Core.Application.Characters;
using OpenTrpg.Core.Application.Common;
using OpenTrpg.Core.Domain.Catalog;
using OpenTrpg.Core.Domain.Characters;
using OpenTrpg.Core.Domain.Items;
using FluentValidation;
using OpenTrpg.Core.Application.Systems;

namespace OpenTrpg.Core.Application.Items;

// ---- Requests ------------------------------------------------------------------------------------

/// <summary>
/// Adds an item to an inventory. Quick creation: <see cref="TemplateId"/> without overrides. Advanced:
/// template plus overrides, or no template and every field in <see cref="Overrides"/> (a name at least).
/// Also the payload of AddItem/CustomItem change requests.
/// </summary>
public sealed record AddInventoryItemRequest
{
    public Guid? TemplateId { get; init; }

    public int Quantity { get; init; } = 1;

    public ItemOverridesDto? Overrides { get; init; }
}

public sealed class AddInventoryItemRequestValidator : AbstractValidator<AddInventoryItemRequest>
{
    public AddInventoryItemRequestValidator()
    {
        RuleFor(x => x.Quantity).InclusiveBetween(1, ItemLimits.MaxQuantity)
            .WithMessage($"La cantidad debe estar entre 1 y {ItemLimits.MaxQuantity}.");
        RuleFor(x => x.Overrides)
            .Must((request, overrides) => request.TemplateId is not null || !string.IsNullOrWhiteSpace(overrides?.Name))
            .WithMessage("Indica un objeto del catálogo o el nombre del objeto personalizado.");
        RuleFor(x => x.Overrides!).SetValidator(new ItemOverridesDtoValidator()).When(x => x.Overrides is not null);
    }
}

/// <summary>Play-state changes: absent fields do not change; <c>notes: null</c> clears the notes and <c>charges: null</c> removes the charges.</summary>
public sealed record UpdateInventoryItemRequest
{
    public bool? Equipped { get; init; }

    public bool? Attuned { get; init; }

    /// <summary>With <c>attuned: true</c>: an attuned item that ends its attunement in the same operation (limit of 3 reached).</summary>
    public Guid? ReplaceAttunedItemId { get; init; }

    public Optional<string?> Notes { get; init; }

    public int? SortOrder { get; init; }

    /// <summary>Remaining charges; the first value given also becomes the maximum.</summary>
    public Optional<int?> Charges { get; init; }
}

public sealed class UpdateInventoryItemRequestValidator : AbstractValidator<UpdateInventoryItemRequest>
{
    public const int MaxSortOrder = 100_000;

    public UpdateInventoryItemRequestValidator()
    {
        RuleFor(x => x.Notes)
            .Must(n => !n.IsSet || n.Value is null || n.Value.Trim().Length <= ItemLimits.NotesMaxLength)
            .WithMessage($"Las notas no pueden superar los {ItemLimits.NotesMaxLength} caracteres.");
        RuleFor(x => x.SortOrder).InclusiveBetween(0, MaxSortOrder).WithMessage($"El orden debe estar entre 0 y {MaxSortOrder}.");
        RuleFor(x => x.Charges)
            .Must(c => !c.IsSet || c.Value is null || c.Value is >= 0 and <= ItemLimits.MaxCharges)
            .WithMessage($"Las cargas deben estar entre 0 y {ItemLimits.MaxCharges}.");
    }
}

/// <param name="Quantity">Units to remove; absent = the whole entry.</param>
public sealed record RemoveInventoryItemRequest(int? Quantity = null);

public sealed class RemoveInventoryItemRequestValidator : AbstractValidator<RemoveInventoryItemRequest>
{
    public RemoveInventoryItemRequestValidator()
    {
        RuleFor(x => x.Quantity).InclusiveBetween(1, ItemLimits.MaxQuantity)
            .WithMessage($"La cantidad debe estar entre 1 y {ItemLimits.MaxQuantity}.");
    }
}

/// <summary>Payload of RemoveItem change requests (the name is kept for the DM to read).</summary>
public sealed record RemoveItemPayload(Guid ItemId, int? Quantity, string ItemName);

/// <summary>Money change outside a purchase or sale, in copper pieces (negative subtracts).</summary>
public sealed record AdjustMoneyRequest(int DeltaCp, string? Reason = null);

public sealed class AdjustMoneyRequestValidator : AbstractValidator<AdjustMoneyRequest>
{
    public const int ReasonMaxLength = 500;

    public AdjustMoneyRequestValidator()
    {
        RuleFor(x => x.DeltaCp)
            .NotEqual(0).WithMessage("Indica una cantidad de dinero distinta de 0.")
            .InclusiveBetween(-Character.MaxMoney, Character.MaxMoney)
            .WithMessage($"La cantidad debe estar entre -{Character.MaxMoney} y {Character.MaxMoney} pc.");
        RuleFor(x => x.Reason).MaximumLength(ReasonMaxLength)
            .WithMessage($"El motivo no puede superar los {ReasonMaxLength} caracteres.");
    }
}

// ---- Shared operations ---------------------------------------------------------------------------

/// <summary>
/// Inventory changes that need approval for the owner of an active character. The same code applies
/// a direct change (DM, or owner of a draft) and an approved change request.
/// </summary>
public sealed class InventoryOperations(
    IItemTemplateRepository templates,
    IChangeRequestRepository changeRequests,
    IValidator<AddInventoryItemRequest> addValidator,
    IValidator<AdjustMoneyRequest> moneyValidator,
    IUnitOfWork unitOfWork,
    ICampaignNotifier notifier,
    IDateTimeProvider clock)
{
    private static readonly JsonSerializerOptions PayloadOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>AddItem for a catalog item without overrides; CustomItem for anything made or changed by hand.</summary>
    public static string AddRequestType(AddInventoryItemRequest request) =>
        request.TemplateId is null || !(request.Overrides?.ToDomain().Normalize().IsEmpty ?? true)
            ? ChangeRequestTypes.CustomItem
            : ChangeRequestTypes.AddItem;

    public async Task<CharacterItem> AddAsync(Character character, AddInventoryItemRequest request, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var (template, overrides) = await ResolveAddAsync(character.CampaignId, request, cancellationToken);
        return character.AddItem(template?.Id, overrides, request.Quantity, EffectiveItem.Resolve(template, overrides), now);
    }

    /// <summary>
    /// Checks what <see cref="AddAsync"/> would check (catalog item usable in the campaign, overrides in
    /// range) without changing anything, so a change request is only created for an applicable item.
    /// </summary>
    public async Task EnsureCanAddAsync(Guid campaignId, AddInventoryItemRequest request, CancellationToken cancellationToken) =>
        await ResolveAddAsync(campaignId, request, cancellationToken);

    /// <summary>
    /// The template (usable in the campaign, 400 otherwise) and the normalized overrides of an item to
    /// add; an item without template needs a name.
    /// </summary>
    public async Task<(ItemTemplate? Template, ItemOverrides Overrides)> ResolveAddAsync(Guid campaignId, AddInventoryItemRequest request, CancellationToken cancellationToken)
    {
        var template = request.TemplateId is { } templateId
            ? await templates.GetVisibleAsync(campaignId, templateId, cancellationToken) ?? throw ItemErrors.UnknownTemplate()
            : null;
        var overrides = (request.Overrides?.ToDomain() ?? ItemOverrides.None()).Normalize();
        if (template is null && overrides.Name is null)
        {
            throw AppException.Validation("overrides.name", "Un objeto sin plantilla necesita un nombre.");
        }

        return (template, overrides);
    }

    /// <summary>Creates a pending change request of <paramref name="type"/> with the payload serialized as JSON.</summary>
    public async Task<ChangeRequestDto> RequestAsync<TPayload>(
        Character character,
        Guid requestedByUserId,
        string type,
        TPayload payload,
        CancellationToken cancellationToken,
        object? before = null)
    {
        var request = ChangeRequest.Create(
            character.CampaignId,
            character.Id,
            requestedByUserId,
            type,
            JsonSerializer.Serialize(payload, PayloadOptions),
            clock.UtcNow,
            before is null ? null : JsonSerializer.Serialize(before, PayloadOptions));
        changeRequests.Add(request);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await notifier.ChangeRequestUpdatedAsync(character.CampaignId, character.Id, request.Id, clock.UtcNow, cancellationToken);
        return ChangeRequestDto.From((await changeRequests.ListViewsAsync(new ChangeRequestQuery(Id: request.Id), cancellationToken)).Single());
    }

    /// <summary>Applies an approved AddItem, CustomItem, RemoveItem or AdjustMoney request to the character.</summary>
    public async Task ApplyApprovedAsync(Character character, ChangeRequest request, DateTimeOffset now, CancellationToken cancellationToken)
    {
        switch (request.Type)
        {
            case ChangeRequestTypes.AddItem or ChangeRequestTypes.CustomItem:
                var add = Deserialize<AddInventoryItemRequest>(request.PayloadJson);
                await ValidateAsync(addValidator, add, cancellationToken);
                await AddAsync(character, add, now, cancellationToken);
                break;
            case ChangeRequestTypes.RemoveItem:
                var remove = Deserialize<RemoveItemPayload>(request.PayloadJson);
                if (!character.Items.Any(i => i.Id == remove.ItemId))
                {
                    throw AppException.Conflict("El objeto ya no está en el inventario.");
                }

                character.RemoveItem(remove.ItemId, remove.Quantity, now);
                break;
            case ChangeRequestTypes.AdjustMoney:
                var money = Deserialize<AdjustMoneyRequest>(request.PayloadJson);
                await ValidateAsync(moneyValidator, money, cancellationToken);
                character.AdjustMoney(money.DeltaCp, now);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(request), request.Type, "Not an inventory change request.");
        }
    }

    private static T Deserialize<T>(string json)
        where T : class
    {
        try
        {
            return JsonSerializer.Deserialize<T>(json, PayloadOptions) ?? throw InvalidPayload();
        }
        catch (JsonException)
        {
            throw InvalidPayload();
        }
    }

    private static async Task ValidateAsync<T>(IValidator<T> validator, T value, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(value, cancellationToken);
        if (!validation.IsValid)
        {
            throw AppException.Validation("payload", validation.Errors[0].ErrorMessage);
        }
    }

    private static AppException InvalidPayload() =>
        AppException.Validation("payload", "El contenido de la solicitud no es una operación de inventario válida.");
}

// ---- Handlers ------------------------------------------------------------------------------------

/// <summary>Inventory of a character: owner and DMs.</summary>
public sealed class GetInventoryHandler(CharacterLoader loader, InventoryReader reader)
{
    public async Task<InventoryDto> HandleAsync(Guid currentUserId, Guid characterId, CancellationToken cancellationToken = default)
    {
        var loaded = await loader.LoadAsync(characterId, currentUserId, cancellationToken);
        if (!loaded.Character.CanViewSheet(currentUserId, loaded.IsDm))
        {
            throw AppException.Forbidden("Solo el dueño del personaje o un DM pueden ver su inventario.");
        }

        return await reader.BuildAsync(loaded.Character, cancellationToken);
    }
}

/// <summary>Outcome of an inventory change: applied (<see cref="Item"/>/<see cref="Inventory"/>) or sent to the DM (<see cref="ChangeRequest"/>).</summary>
public sealed record InventoryChangeResult(CharacterItemDto? Item, InventoryDto? Inventory, ChangeRequestDto? ChangeRequest);

/// <summary>
/// Adds an item: DMs and the owner of a draft directly (201); the owner of an active character creates
/// an AddItem (catalog item) or CustomItem (with overrides or without template) change request (202).
/// </summary>
public sealed class AddInventoryItemHandler(
    CharacterLoader loader,
    InventoryOperations operations,
    InventoryReader reader,
    IUnitOfWork unitOfWork,
    ICampaignNotifier notifier,
    IDateTimeProvider clock)
{
    public async Task<InventoryChangeResult> HandleAsync(Guid currentUserId, Guid characterId, AddInventoryItemRequest request, CancellationToken cancellationToken = default)
    {
        var loaded = await loader.LoadAsync(characterId, currentUserId, cancellationToken);
        var character = loaded.Character;
        var mode = character.ResolveSheetEdit(currentUserId, loaded.IsDm);

        if (mode == SheetEditMode.Direct)
        {
            var item = await operations.AddAsync(character, request, clock.UtcNow, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await notifier.CharacterUpdatedAsync(character.CampaignId, character.Id, clock.UtcNow, cancellationToken);
            return new InventoryChangeResult(await reader.BuildItemAsync(item, cancellationToken), null, null);
        }

        var (template, _) = await operations.ResolveAddAsync(character.CampaignId, request, cancellationToken);
        var before = template is null ? null : new { template = ItemDetailDto.From(template) };
        var changeRequest = await operations.RequestAsync(character, currentUserId, InventoryOperations.AddRequestType(request), request, cancellationToken, before);
        return new InventoryChangeResult(null, null, changeRequest);
    }
}

/// <summary>
/// Equip, attune, notes, order and charges: owner and DMs, without approval. Equipping or attuning
/// recalculates the sheet (item modifiers), capping the current hit points at the new maximum.
/// </summary>
public sealed class UpdateInventoryItemHandler(
    CharacterLoader loader,
    InventoryReader reader,
    InventoryHooks hooks,
    IUnitOfWork unitOfWork,
    ICampaignNotifier notifier,
    IDateTimeProvider clock)
{
    public async Task<CharacterItemDto> HandleAsync(Guid currentUserId, Guid characterId, Guid itemId, UpdateInventoryItemRequest request, CancellationToken cancellationToken = default)
    {
        var loaded = await loader.LoadAsync(characterId, currentUserId, cancellationToken);
        var character = loaded.Character;
        character.EnsureCanTrack(currentUserId, loaded.IsDm);

        var templates = await reader.TemplatesAsync(character, cancellationToken);
        var item = character.UpdateItem(
            itemId,
            new ItemUpdate
            {
                Equipped = request.Equipped,
                Attuned = request.Attuned,
                ReplaceAttunedItemId = request.ReplaceAttunedItemId,
                SetNotes = request.Notes.IsSet,
                Notes = request.Notes.Value,
                SortOrder = request.SortOrder,
                SetCharges = request.Charges.IsSet,
                Charges = request.Charges.Value,
            },
            i => InventoryView.Resolve(templates, i),
            clock.UtcNow);
        if (request.Equipped is not null || request.Attuned is not null)
        {
            await hooks.ChangedAsync(character, InventoryChangeKinds.Updated, item.Id, cancellationToken);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await notifier.CharacterUpdatedAsync(character.CampaignId, character.Id, clock.UtcNow, cancellationToken);
        return CharacterItemDto.From(item, InventoryView.TemplateOf(templates, item.TemplateId));
    }
}

/// <summary>Uses an item (a charge, or a unit of a consumable): owner and DMs, without approval. Returns the inventory.</summary>
public sealed class UseInventoryItemHandler(CharacterLoader loader, InventoryReader reader, IUnitOfWork unitOfWork, ICampaignNotifier notifier, IDateTimeProvider clock)
{
    public async Task<InventoryDto> HandleAsync(Guid currentUserId, Guid characterId, Guid itemId, AmountRequest? request, CancellationToken cancellationToken = default)
    {
        var loaded = await loader.LoadAsync(characterId, currentUserId, cancellationToken);
        var character = loaded.Character;
        character.EnsureCanTrack(currentUserId, loaded.IsDm);

        var templates = await reader.TemplatesAsync(character, cancellationToken);
        var item = character.FindItem(itemId);
        character.UseItem(itemId, request?.Amount ?? 1, InventoryView.Resolve(templates, item), clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await notifier.CharacterUpdatedAsync(character.CampaignId, character.Id, clock.UtcNow, cancellationToken);
        return await reader.BuildAsync(character, cancellationToken);
    }
}

/// <summary>
/// Removes units of an item: DMs and the owner of a draft directly (removing an equipped entry
/// recalculates the sheet); the owner of an active character via a RemoveItem request.
/// </summary>
public sealed class RemoveInventoryItemHandler(
    CharacterLoader loader,
    InventoryOperations operations,
    InventoryReader reader,
    InventoryHooks hooks,
    IUnitOfWork unitOfWork,
    ICampaignNotifier notifier,
    IDateTimeProvider clock)
{
    public async Task<ChangeRequestDto?> HandleAsync(Guid currentUserId, Guid characterId, Guid itemId, RemoveInventoryItemRequest? request, CancellationToken cancellationToken = default)
    {
        var loaded = await loader.LoadAsync(characterId, currentUserId, cancellationToken);
        var character = loaded.Character;
        var mode = character.ResolveSheetEdit(currentUserId, loaded.IsDm);
        var item = character.FindItem(itemId);
        var quantity = request?.Quantity;
        if (quantity > item.Quantity)
        {
            throw AppException.Validation("quantity", $"Solo hay {item.Quantity} unidades de este objeto.");
        }

        if (mode == SheetEditMode.Direct)
        {
            var wasEquipped = item.Equipped;
            character.RemoveItem(itemId, quantity, clock.UtcNow);
            if (wasEquipped)
            {
                await hooks.ChangedAsync(character, InventoryChangeKinds.Removed, itemId, cancellationToken);
            }

            await unitOfWork.SaveChangesAsync(cancellationToken);
            await notifier.CharacterUpdatedAsync(character.CampaignId, character.Id, clock.UtcNow, cancellationToken);
            return null;
        }

        var templates = await reader.TemplatesAsync(character, cancellationToken);
        var payload = new RemoveItemPayload(itemId, quantity, InventoryView.Resolve(templates, item).Name);
        return await operations.RequestAsync(character, currentUserId, ChangeRequestTypes.RemoveItem, payload, cancellationToken, new { quantity = item.Quantity });
    }
}

/// <summary>Money outside purchases and sales: DMs and the owner of a draft directly; the owner of an active character via an AdjustMoney request.</summary>
public sealed class AdjustMoneyHandler(
    CharacterLoader loader,
    InventoryOperations operations,
    InventoryReader reader,
    IUnitOfWork unitOfWork,
    ICampaignNotifier notifier,
    IDateTimeProvider clock)
{
    public async Task<InventoryChangeResult> HandleAsync(Guid currentUserId, Guid characterId, AdjustMoneyRequest request, CancellationToken cancellationToken = default)
    {
        var loaded = await loader.LoadAsync(characterId, currentUserId, cancellationToken);
        var character = loaded.Character;
        var mode = character.ResolveSheetEdit(currentUserId, loaded.IsDm);

        if (mode == SheetEditMode.Direct)
        {
            character.AdjustMoney(request.DeltaCp, clock.UtcNow);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await notifier.CharacterUpdatedAsync(character.CampaignId, character.Id, clock.UtcNow, cancellationToken);
            return new InventoryChangeResult(null, await reader.BuildAsync(character, cancellationToken), null);
        }

        if (character.Money + (long)request.DeltaCp is < 0 or > Character.MaxMoney)
        {
            throw AppException.Validation("deltaCp", "El dinero resultante quedaría fuera de rango.");
        }

        var trimmed = request with { Reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim() };
        var changeRequest = await operations.RequestAsync(character, currentUserId, ChangeRequestTypes.AdjustMoney, trimmed, cancellationToken, new { copperPieces = character.Money });
        return new InventoryChangeResult(null, null, changeRequest);
    }
}
