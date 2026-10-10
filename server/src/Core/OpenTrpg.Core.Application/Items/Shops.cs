using OpenTrpg.Core.Application.Abstractions;
using OpenTrpg.Core.Application.Abstractions.Persistence;
using OpenTrpg.Core.Application.Common;
using OpenTrpg.Core.Domain.Campaigns;
using OpenTrpg.Core.Domain.Catalog;
using OpenTrpg.Core.Domain.Items;
using FluentValidation;

namespace OpenTrpg.Core.Application.Items;

// ---- Requests ------------------------------------------------------------------------------------

/// <param name="BuybackPercent">Percentage paid when buying from characters (default 50).</param>
public sealed record CreateShopRequest(string Name, string? Description = null, int? BuybackPercent = null);

public sealed class CreateShopRequestValidator : AbstractValidator<CreateShopRequest>
{
    public CreateShopRequestValidator()
    {
        RuleFor(x => x.Name)
            .Must(n => !string.IsNullOrWhiteSpace(n)).WithMessage("Indica el nombre de la tienda.")
            .Must(n => n is null || n.Trim().Length <= Shop.NameMaxLength)
            .WithMessage($"El nombre no puede superar los {Shop.NameMaxLength} caracteres.");
        RuleFor(x => x.Description).MaximumLength(Shop.DescriptionMaxLength)
            .WithMessage($"La descripción no puede superar los {Shop.DescriptionMaxLength} caracteres.");
        RuleFor(x => x.BuybackPercent).InclusiveBetween(0, 100).WithMessage("El porcentaje de recompra debe estar entre 0 y 100.");
    }
}

/// <summary>Absent (null) fields do not change; an empty description clears it.</summary>
public sealed record UpdateShopRequest(string? Name = null, string? Description = null, bool? IsOpen = null, int? BuybackPercent = null);

public sealed class UpdateShopRequestValidator : AbstractValidator<UpdateShopRequest>
{
    public UpdateShopRequestValidator()
    {
        RuleFor(x => x.Name)
            .Must(n => !string.IsNullOrWhiteSpace(n)).WithMessage("El nombre no puede estar vacío.")
            .Must(n => n!.Trim().Length <= Shop.NameMaxLength).WithMessage($"El nombre no puede superar los {Shop.NameMaxLength} caracteres.")
            .When(x => x.Name is not null);
        RuleFor(x => x.Description).MaximumLength(Shop.DescriptionMaxLength)
            .WithMessage($"La descripción no puede superar los {Shop.DescriptionMaxLength} caracteres.");
        RuleFor(x => x.BuybackPercent).InclusiveBetween(0, 100).WithMessage("El porcentaje de recompra debe estar entre 0 y 100.");
    }
}

/// <param name="PriceCp">Unit price in copper pieces.</param>
/// <param name="Stock">Units available; null = unlimited.</param>
public sealed record AddShopItemRequest(Guid? TemplateId, ItemOverridesDto? Overrides, int PriceCp, int? Stock = null);

public sealed class AddShopItemRequestValidator : AbstractValidator<AddShopItemRequest>
{
    public AddShopItemRequestValidator()
    {
        RuleFor(x => x.Overrides)
            .Must((request, overrides) => request.TemplateId is not null || !string.IsNullOrWhiteSpace(overrides?.Name))
            .WithMessage("Indica un objeto del catálogo o el nombre del objeto personalizado.");
        RuleFor(x => x.Overrides!).SetValidator(new ItemOverridesDtoValidator()).When(x => x.Overrides is not null);
        RuleFor(x => x.PriceCp).InclusiveBetween(0, ItemLimits.MaxCostCp).WithMessage($"El precio debe estar entre 0 y {ItemLimits.MaxCostCp} pc.");
        RuleFor(x => x.Stock).InclusiveBetween(0, ItemLimits.MaxStock).WithMessage($"El stock debe estar entre 0 y {ItemLimits.MaxStock}.");
    }
}

/// <param name="TemplateId">Catalog item (SRD, content pack or homebrew of the campaign).</param>
/// <param name="PriceCp">Unit price in copper pieces; null = the template's list price (0 when it has none).</param>
/// <param name="Stock">Units available; null = unlimited.</param>
public sealed record BulkShopItem(Guid TemplateId, int? PriceCp = null, int? Stock = null);

/// <summary>Several catalog items added to a shop at once (all or nothing).</summary>
public sealed record AddShopItemsBulkRequest(IReadOnlyList<BulkShopItem> Items)
{
    /// <summary>Most items accepted in one request.</summary>
    public const int MaxItems = 200;
}

public sealed class AddShopItemsBulkRequestValidator : AbstractValidator<AddShopItemsBulkRequest>
{
    public AddShopItemsBulkRequestValidator()
    {
        RuleFor(x => x.Items)
            .NotNull().WithMessage("Indica los objetos que quieres añadir.")
            .Must(items => items is { Count: > 0 and <= AddShopItemsBulkRequest.MaxItems })
            .WithMessage($"Añade entre 1 y {AddShopItemsBulkRequest.MaxItems} objetos a la vez.");
        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.TemplateId).NotEmpty().WithMessage("Indica un objeto del catálogo.");
            item.RuleFor(i => i.PriceCp).InclusiveBetween(0, ItemLimits.MaxCostCp).WithMessage($"El precio debe estar entre 0 y {ItemLimits.MaxCostCp} pc.");
            item.RuleFor(i => i.Stock).InclusiveBetween(0, ItemLimits.MaxStock).WithMessage($"El stock debe estar entre 0 y {ItemLimits.MaxStock}.");
        }).When(x => x.Items is not null);
    }
}

/// <summary>
/// Absent fields do not change; <c>stock: null</c> makes the stock unlimited; <see cref="Overrides"/>
/// replaces every override when given.
/// </summary>
public sealed record UpdateShopItemRequest
{
    public int? PriceCp { get; init; }

    public Optional<int?> Stock { get; init; }

    public ItemOverridesDto? Overrides { get; init; }

    public int? SortOrder { get; init; }
}

public sealed class UpdateShopItemRequestValidator : AbstractValidator<UpdateShopItemRequest>
{
    public UpdateShopItemRequestValidator()
    {
        RuleFor(x => x.PriceCp).InclusiveBetween(0, ItemLimits.MaxCostCp).WithMessage($"El precio debe estar entre 0 y {ItemLimits.MaxCostCp} pc.");
        RuleFor(x => x.Stock)
            .Must(s => !s.IsSet || s.Value is null || s.Value is >= 0 and <= ItemLimits.MaxStock)
            .WithMessage($"El stock debe estar entre 0 y {ItemLimits.MaxStock}.");
        RuleFor(x => x.Overrides!).SetValidator(new ItemOverridesDtoValidator()).When(x => x.Overrides is not null);
        RuleFor(x => x.SortOrder).InclusiveBetween(0, UpdateInventoryItemRequestValidator.MaxSortOrder)
            .WithMessage($"El orden debe estar entre 0 y {UpdateInventoryItemRequestValidator.MaxSortOrder}.");
    }
}

// ---- Loading and DTOs ----------------------------------------------------------------------------

/// <summary>A shop loaded for a use case, with the role of the acting user in its campaign.</summary>
public sealed record LoadedShop(Shop Shop, CampaignRole Role)
{
    public bool IsDm => Role.IsAtLeast(CampaignRole.DM);
}

/// <summary>Loads shops (tracked, with their items) and builds their DTOs.</summary>
public sealed class ShopLoader(IShopRepository shops, ICampaignAccess access, IItemTemplateRepository templates)
{
    /// <summary>
    /// 404 when the shop does not exist or the actor is not a member of its campaign, and also for
    /// closed shops when <paramref name="hideClosedFromPlayers"/> and the actor is a player.
    /// </summary>
    public async Task<LoadedShop> LoadAsync(Guid shopId, Guid actorUserId, bool hideClosedFromPlayers, CancellationToken cancellationToken)
    {
        var shop = await shops.GetWithItemsAsync(shopId, cancellationToken) ?? throw ItemErrors.ShopNotFound();
        var role = await access.GetRoleAsync(shop.CampaignId, actorUserId, cancellationToken) ?? throw ItemErrors.ShopNotFound();
        var loaded = new LoadedShop(shop, role);
        if (hideClosedFromPlayers && !loaded.IsDm && !shop.IsOpen)
        {
            throw ItemErrors.ShopNotFound();
        }

        return loaded;
    }

    /// <summary>Like <see cref="LoadAsync"/> but only for DMs (403 for players).</summary>
    public async Task<Shop> LoadForDmAsync(Guid shopId, Guid actorUserId, CancellationToken cancellationToken)
    {
        var loaded = await LoadAsync(shopId, actorUserId, hideClosedFromPlayers: false, cancellationToken);
        return loaded.IsDm ? loaded.Shop : throw AppException.Forbidden("Solo un DM puede gestionar las tiendas.");
    }

    public async Task<ShopDto> ToDtoAsync(Shop shop, CancellationToken cancellationToken)
    {
        var loaded = await InventoryView.LoadTemplatesAsync(templates, shop.Items.Select(i => i.TemplateId), cancellationToken);
        return new ShopDto(
            shop.Id,
            shop.CampaignId,
            shop.Name,
            shop.Description,
            shop.IsOpen,
            shop.BuybackPercent,
            shop.Items
                .OrderBy(i => i.SortOrder)
                .ThenBy(i => i.Id)
                .Select(i => ShopItemDto.From(i, InventoryView.TemplateOf(loaded, i.TemplateId)))
                .ToList());
    }

    public async Task<ShopItemDto> ToDtoAsync(ShopItem item, CancellationToken cancellationToken)
    {
        var template = item.TemplateId is { } id ? (await templates.ListByIdsAsync([id], cancellationToken)).SingleOrDefault() : null;
        return ShopItemDto.From(item, template);
    }

    /// <summary>The template of a new shop item, which must be usable in the campaign (400 otherwise).</summary>
    public async Task<ItemTemplate?> VisibleTemplateAsync(Guid campaignId, Guid? templateId, CancellationToken cancellationToken) =>
        templateId is { } id
            ? await templates.GetSelectableAsync(campaignId, id, cancellationToken) ?? throw ItemErrors.UnknownTemplate()
            : null;
}

// ---- Handlers ------------------------------------------------------------------------------------

/// <summary>Shops of the campaign by name: DMs see all of them, players only the open ones.</summary>
public sealed class ListShopsHandler(ICampaignAccess access, IShopRepository shops)
{
    public async Task<IReadOnlyList<ShopSummaryDto>> HandleAsync(Guid currentUserId, Guid campaignId, CancellationToken cancellationToken = default)
    {
        var role = await access.RequireAsync(campaignId, currentUserId, CampaignRole.Player, cancellationToken);
        var list = await shops.ListByCampaignAsync(campaignId, openOnly: !role.IsAtLeast(CampaignRole.DM), cancellationToken);
        return list
            .Select(s => new ShopSummaryDto(s.Id, s.CampaignId, s.Name, s.Description, s.IsOpen, s.BuybackPercent, s.Items.Count))
            .ToList();
    }
}

/// <summary>A DM creates a (closed) shop.</summary>
public sealed class CreateShopHandler(ICampaignAccess access, IShopRepository shops, ShopLoader loader, IUnitOfWork unitOfWork, IDateTimeProvider clock)
{
    public async Task<ShopDto> HandleAsync(Guid currentUserId, Guid campaignId, CreateShopRequest request, CancellationToken cancellationToken = default)
    {
        await access.RequireAsync(campaignId, currentUserId, CampaignRole.DM, cancellationToken);
        var shop = Shop.Create(campaignId, request.Name, request.Description, request.BuybackPercent, clock.UtcNow);
        shops.Add(shop);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await loader.ToDtoAsync(shop, cancellationToken);
    }
}

/// <summary>A shop with its items, effective data and prices. Players only see open shops (404 otherwise).</summary>
public sealed class GetShopHandler(ShopLoader loader)
{
    public async Task<ShopDto> HandleAsync(Guid currentUserId, Guid shopId, CancellationToken cancellationToken = default)
    {
        var loaded = await loader.LoadAsync(shopId, currentUserId, hideClosedFromPlayers: true, cancellationToken);
        return await loader.ToDtoAsync(loaded.Shop, cancellationToken);
    }
}

public sealed class UpdateShopHandler(ShopLoader loader, IUnitOfWork unitOfWork, ICampaignNotifier notifier, IDateTimeProvider clock)
{
    public async Task<ShopDto> HandleAsync(Guid currentUserId, Guid shopId, UpdateShopRequest request, CancellationToken cancellationToken = default)
    {
        var shop = await loader.LoadForDmAsync(shopId, currentUserId, cancellationToken);
        shop.Update(request.Name, request.Description, request.IsOpen, request.BuybackPercent, clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await notifier.NotifyAsync(new CampaignEvent(CampaignEventTypes.ShopUpdated, shop.CampaignId, null, shop.Id, clock.UtcNow), cancellationToken);
        return await loader.ToDtoAsync(shop, cancellationToken);
    }
}

/// <summary>A DM deletes a shop with its items and transactions.</summary>
public sealed class DeleteShopHandler(ShopLoader loader, IShopRepository shops, IUnitOfWork unitOfWork)
{
    public async Task HandleAsync(Guid currentUserId, Guid shopId, CancellationToken cancellationToken = default)
    {
        var shop = await loader.LoadForDmAsync(shopId, currentUserId, cancellationToken);
        shops.Remove(shop);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

public sealed class AddShopItemHandler(ShopLoader loader, IUnitOfWork unitOfWork, IDateTimeProvider clock)
{
    public async Task<ShopItemDto> HandleAsync(Guid currentUserId, Guid shopId, AddShopItemRequest request, CancellationToken cancellationToken = default)
    {
        var shop = await loader.LoadForDmAsync(shopId, currentUserId, cancellationToken);
        var template = await loader.VisibleTemplateAsync(shop.CampaignId, request.TemplateId, cancellationToken);
        var item = shop.AddItem(template?.Id, request.Overrides?.ToDomain() ?? ItemOverrides.None(), request.PriceCp, request.Stock, clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ShopItemDto.From(item, template);
    }
}

/// <summary>
/// A DM adds several catalog items to a shop in one operation, each one at its given price or the
/// template's list price. Every template must be usable in the campaign (400 otherwise, nothing added).
/// </summary>
public sealed class AddShopItemsBulkHandler(ShopLoader loader, IItemTemplateRepository templates, IUnitOfWork unitOfWork, IDateTimeProvider clock)
{
    public async Task<ShopDto> HandleAsync(Guid currentUserId, Guid shopId, AddShopItemsBulkRequest request, CancellationToken cancellationToken = default)
    {
        var shop = await loader.LoadForDmAsync(shopId, currentUserId, cancellationToken);
        var loaded = (await templates.ListByIdsAsync(request.Items.Select(i => i.TemplateId).Distinct().ToList(), cancellationToken))
            .Where(t => t.CampaignId is null || t.CampaignId == shop.CampaignId)
            .ToDictionary(t => t.Id);
        if (request.Items.Any(i => !loaded.ContainsKey(i.TemplateId)))
        {
            throw ItemErrors.UnknownTemplate();
        }

        var now = clock.UtcNow;
        foreach (var entry in request.Items)
        {
            var template = loaded[entry.TemplateId];
            shop.AddItem(template.Id, ItemOverrides.None(), entry.PriceCp ?? template.Cost ?? 0, entry.Stock, now);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await loader.ToDtoAsync(shop, cancellationToken);
    }
}

public sealed class UpdateShopItemHandler(ShopLoader loader, IUnitOfWork unitOfWork)
{
    public async Task<ShopItemDto> HandleAsync(Guid currentUserId, Guid shopId, Guid shopItemId, UpdateShopItemRequest request, CancellationToken cancellationToken = default)
    {
        var shop = await loader.LoadForDmAsync(shopId, currentUserId, cancellationToken);
        var item = shop.FindItem(shopItemId);
        item.Update(request.PriceCp, request.Stock.IsSet, request.Stock.Value, request.Overrides?.ToDomain(), request.SortOrder);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await loader.ToDtoAsync(item, cancellationToken);
    }
}

public sealed class DeleteShopItemHandler(ShopLoader loader, IUnitOfWork unitOfWork, IDateTimeProvider clock)
{
    public async Task HandleAsync(Guid currentUserId, Guid shopId, Guid shopItemId, CancellationToken cancellationToken = default)
    {
        var shop = await loader.LoadForDmAsync(shopId, currentUserId, cancellationToken);
        shop.RemoveItem(shopItemId, clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
