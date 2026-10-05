using Dnd.Application.Abstractions;
using Dnd.Application.Abstractions.Persistence;
using Dnd.Application.Characters;
using Dnd.Application.Common;
using Dnd.Domain.Campaigns;
using Dnd.Domain.Characters;
using Dnd.Domain.Items;
using FluentValidation;

namespace Dnd.Application.Items;

public sealed record BuyRequest(Guid CharacterId, Guid ShopItemId, int Quantity = 1);

public sealed class BuyRequestValidator : AbstractValidator<BuyRequest>
{
    public BuyRequestValidator()
    {
        RuleFor(x => x.CharacterId).NotEmpty().WithMessage("Indica el personaje.");
        RuleFor(x => x.ShopItemId).NotEmpty().WithMessage("Indica el objeto de la tienda.");
        RuleFor(x => x.Quantity).InclusiveBetween(1, ItemLimits.MaxQuantity)
            .WithMessage($"La cantidad debe estar entre 1 y {ItemLimits.MaxQuantity}.");
    }
}

/// <param name="ItemId">Inventory entry of the character to sell.</param>
public sealed record SellRequest(Guid CharacterId, Guid ItemId, int Quantity = 1);

public sealed class SellRequestValidator : AbstractValidator<SellRequest>
{
    public SellRequestValidator()
    {
        RuleFor(x => x.CharacterId).NotEmpty().WithMessage("Indica el personaje.");
        RuleFor(x => x.ItemId).NotEmpty().WithMessage("Indica el objeto que quieres vender.");
        RuleFor(x => x.Quantity).InclusiveBetween(1, ItemLimits.MaxQuantity)
            .WithMessage($"La cantidad debe estar entre 1 y {ItemLimits.MaxQuantity}.");
    }
}

/// <summary>
/// Loads both sides of a trade: the shop (tracked, with items; closed shops are not hidden here, the
/// trade itself answers 409) and a character of the same campaign (tracked, with its inventory) that
/// the actor owns or, as DM, manages.
/// </summary>
public sealed class TradeLoader(ShopLoader shops, ICharacterRepository characters)
{
    public async Task<(Shop Shop, Character Character)> LoadAsync(Guid shopId, Guid characterId, Guid actorUserId, CancellationToken cancellationToken)
    {
        var loaded = await shops.LoadAsync(shopId, actorUserId, hideClosedFromPlayers: false, cancellationToken);
        var character = await characters.GetWithDetailsAsync(characterId, cancellationToken);
        if (character is null || character.CampaignId != loaded.Shop.CampaignId)
        {
            throw AppException.Validation("characterId", "El personaje no existe en la campaña de la tienda.");
        }

        if (!character.CanViewSheet(actorUserId, loaded.IsDm))
        {
            throw AppException.Forbidden("Solo puedes comerciar con tus propios personajes.");
        }

        return (loaded.Shop, character);
    }
}

/// <summary>
/// A character buys from an open shop. Atomic: stock, money, inventory and the transaction record are
/// saved by a single <c>SaveChanges</c> (one database transaction); the versions of the character and
/// the shop item make a concurrent purchase of the same stock or money fail with 409.
/// </summary>
public sealed class BuyHandler(
    TradeLoader loader,
    IItemTemplateRepository templates,
    ITransactionRepository transactions,
    InventoryReader inventory,
    IUnitOfWork unitOfWork,
    IDateTimeProvider clock)
{
    public async Task<TradeResultDto> HandleAsync(Guid currentUserId, Guid shopId, BuyRequest request, CancellationToken cancellationToken = default)
    {
        var (shop, character) = await loader.LoadAsync(shopId, request.CharacterId, currentUserId, cancellationToken);
        shop.EnsureOpen();
        var shopItem = shop.FindItem(request.ShopItemId);
        var template = shopItem.TemplateId is { } templateId ? (await templates.ListByIdsAsync([templateId], cancellationToken)).SingleOrDefault() : null;
        var effective = EffectiveItem.Resolve(template, shopItem.Overrides);
        var now = clock.UtcNow;

        var total = shop.SellToCharacter(shopItem.Id, request.Quantity, now);
        if (total > character.CopperPieces)
        {
            throw AppException.Validation("quantity", "El personaje no tiene dinero suficiente.");
        }

        character.AdjustMoney(-total, now);
        character.AddItem(shopItem.TemplateId, shopItem.Overrides.Copy(), request.Quantity, effective, now);
        var transaction = Transaction.Record(shop.CampaignId, shop.Id, character.Id, TransactionType.Purchase, effective.Name, request.Quantity, total, now);
        transactions.Add(transaction);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new TradeResultDto(await inventory.BuildAsync(character, cancellationToken), TransactionDto.From(transaction, shop.Name, character.Name));
    }
}

/// <summary>
/// A character sells an item (not attuned) to an open shop for <see cref="Shop.BuybackPercent"/> of its
/// reference price: the price of the matching shop item, or the template cost. Atomic like <see cref="BuyHandler"/>.
/// </summary>
public sealed class SellHandler(
    TradeLoader loader,
    ITransactionRepository transactions,
    InventoryReader inventory,
    IUnitOfWork unitOfWork,
    IDateTimeProvider clock)
{
    public async Task<TradeResultDto> HandleAsync(Guid currentUserId, Guid shopId, SellRequest request, CancellationToken cancellationToken = default)
    {
        var (shop, character) = await loader.LoadAsync(shopId, request.CharacterId, currentUserId, cancellationToken);
        shop.EnsureOpen();
        var item = character.FindItem(request.ItemId);
        if (item.Attuned)
        {
            throw AppException.Validation("itemId", "No se puede vender un objeto sintonizado.");
        }

        if (request.Quantity > item.Quantity)
        {
            throw AppException.Validation("quantity", $"Solo hay {item.Quantity} unidades de este objeto.");
        }

        var loadedTemplates = await inventory.TemplatesAsync(character, cancellationToken);
        var template = InventoryView.TemplateOf(loadedTemplates, item.TemplateId);
        var effective = EffectiveItem.Resolve(template, item.Overrides);
        var matching = shop.FindMatching(item.TemplateId);
        var referencePrice = matching?.PriceCp ?? template?.CostCp
            ?? throw AppException.Validation("itemId", "La tienda no puede tasar este objeto: no tiene precio de referencia.");
        var now = clock.UtcNow;

        var total = shop.BuyFromCharacter(matching, referencePrice, request.Quantity, now);
        character.RemoveItem(item.Id, request.Quantity, now);
        character.AdjustMoney(total, now);
        var transaction = Transaction.Record(shop.CampaignId, shop.Id, character.Id, TransactionType.Sale, effective.Name, request.Quantity, total, now);
        transactions.Add(transaction);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new TradeResultDto(await inventory.BuildAsync(character, cancellationToken), TransactionDto.From(transaction, shop.Name, character.Name));
    }
}

public sealed record ListTransactionsQuery(Guid? CharacterId, int? Page, int? PageSize);

public sealed class ListTransactionsQueryValidator : AbstractValidator<ListTransactionsQuery>
{
    public const int DefaultPageSize = 50;
    public const int MaxPageSize = 200;

    public ListTransactionsQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1).WithMessage("La página debe ser 1 o mayor.");
        RuleFor(x => x.PageSize).InclusiveBetween(1, MaxPageSize).WithMessage($"El tamaño de página debe estar entre 1 y {MaxPageSize}.");
    }
}

/// <summary>Transactions of the campaign, newest first: DMs see all of them, players those of their characters.</summary>
public sealed class ListTransactionsHandler(ICampaignAccess access, ITransactionRepository transactions)
{
    public async Task<PagedResult<TransactionDto>> HandleAsync(Guid currentUserId, Guid campaignId, ListTransactionsQuery query, CancellationToken cancellationToken = default)
    {
        var role = await access.RequireAsync(campaignId, currentUserId, CampaignRole.Player, cancellationToken);
        var page = query.Page ?? 1;
        var pageSize = query.PageSize ?? ListTransactionsQueryValidator.DefaultPageSize;
        var (items, total) = await transactions.ListViewsAsync(
            new TransactionQuery(
                campaignId,
                query.CharacterId,
                role.IsAtLeast(CampaignRole.DM) ? null : currentUserId),
            (page - 1) * pageSize,
            pageSize,
            cancellationToken);
        return new PagedResult<TransactionDto>(
            items.Select(v => TransactionDto.From(v.Transaction, v.ShopName, v.CharacterName)).ToList(),
            total,
            page,
            pageSize);
    }
}
