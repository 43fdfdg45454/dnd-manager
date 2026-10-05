using Dnd.Application.Abstractions;
using Dnd.Application.Abstractions.Persistence;
using Dnd.Application.Catalog;
using Dnd.Application.Common;
using Dnd.Domain.Campaigns;
using Dnd.Domain.Catalog;
using FluentValidation;

namespace Dnd.Application.Items;

public static class ItemErrors
{
    public static AppException ItemNotFound() => AppException.NotFound("Objeto no encontrado.");

    public static AppException ShopNotFound() => AppException.NotFound("Tienda no encontrada.");

    public static AppException UnknownTemplate() =>
        AppException.Validation("templateId", "El objeto no existe en el catálogo de la campaña.");
}

/// <param name="Source">"all" (default), "srd" or "homebrew".</param>
public sealed record SearchCampaignItemsQuery(string? Search, string? Category, string? Rarity, string? Source, int? Page, int? PageSize);

public sealed class SearchCampaignItemsQueryValidator : AbstractValidator<SearchCampaignItemsQuery>
{
    public SearchCampaignItemsQueryValidator()
    {
        RuleFor(x => x.Search).MaximumLength(CatalogQueryDefaults.SearchMaxLength).WithMessage("La búsqueda es demasiado larga.");
        RuleFor(x => x.Category)
            .Must(c => CatalogQueryDefaults.TryParseEnum<ItemCategory>(c, out _))
            .When(x => !string.IsNullOrWhiteSpace(x.Category))
            .WithMessage($"La categoría debe ser una de: {string.Join(", ", Enum.GetNames<ItemCategory>())}.");
        RuleFor(x => x.Rarity)
            .Must(r => CatalogQueryDefaults.TryParseEnum<ItemRarity>(r, out _))
            .When(x => !string.IsNullOrWhiteSpace(x.Rarity))
            .WithMessage($"La rareza debe ser una de: {string.Join(", ", Enum.GetNames<ItemRarity>())}.");
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1).WithMessage("La página debe ser 1 o mayor.");
        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, CatalogQueryDefaults.MaxPageSize)
            .WithMessage($"El tamaño de página debe estar entre 1 y {CatalogQueryDefaults.MaxPageSize}.");
        RuleFor(x => x.Source)
            .Must(s => CatalogQueryDefaults.TryParseEnum<ItemSource>(s, out _))
            .When(x => !string.IsNullOrWhiteSpace(x.Source))
            .WithMessage("El origen debe ser all, srd o homebrew.");
    }
}

/// <summary>Paged search of the items usable in a campaign: SRD plus the campaign's homebrew (members).</summary>
public sealed class SearchCampaignItemsHandler(ICampaignAccess access, IItemTemplateRepository templates)
{
    public async Task<PagedResult<ItemSummaryDto>> HandleAsync(Guid currentUserId, Guid campaignId, SearchCampaignItemsQuery query, CancellationToken cancellationToken = default)
    {
        await access.RequireAsync(campaignId, currentUserId, CampaignRole.Player, cancellationToken);

        var page = query.Page ?? 1;
        var pageSize = query.PageSize ?? CatalogQueryDefaults.DefaultPageSize;
        var filter = new ItemFilter(
            CatalogQueryDefaults.NormalizeSearch(query.Search),
            CatalogQueryDefaults.TryParseEnum<ItemCategory>(query.Category, out var category) ? category : null,
            CatalogQueryDefaults.TryParseEnum<ItemRarity>(query.Rarity, out var rarity) ? rarity : null);
        var source = CatalogQueryDefaults.TryParseEnum<ItemSource>(query.Source, out var parsed) ? parsed : ItemSource.All;

        var (items, total) = await templates.SearchAsync(campaignId, filter, source, (page - 1) * pageSize, pageSize, cancellationToken);
        return new PagedResult<ItemSummaryDto>(items.Select(ItemSummaryDto.From).ToList(), total, page, pageSize);
    }
}

/// <summary>Detail of an item usable in the campaign, SRD or homebrew (members).</summary>
public sealed class GetCampaignItemHandler(ICampaignAccess access, IItemTemplateRepository templates)
{
    public async Task<ItemDetailDto> HandleAsync(Guid currentUserId, Guid campaignId, Guid templateId, CancellationToken cancellationToken = default)
    {
        await access.RequireAsync(campaignId, currentUserId, CampaignRole.Player, cancellationToken);
        var template = await templates.GetVisibleAsync(campaignId, templateId, cancellationToken) ?? throw ItemErrors.ItemNotFound();
        return ItemDetailDto.From(template);
    }
}

/// <summary>A DM creates a homebrew item of the campaign.</summary>
public sealed class CreateHomebrewItemHandler(ICampaignAccess access, IItemTemplateRepository templates, IUnitOfWork unitOfWork, IDateTimeProvider clock)
{
    public async Task<ItemDetailDto> HandleAsync(Guid currentUserId, Guid campaignId, ItemTemplateInput input, CancellationToken cancellationToken = default)
    {
        await access.RequireAsync(campaignId, currentUserId, CampaignRole.DM, cancellationToken);
        var template = ItemTemplate.CreateHomebrew(campaignId, input.ToData(), clock.UtcNow);
        templates.Add(template);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ItemDetailDto.From(template);
    }
}

/// <summary>A DM edits a homebrew item of the campaign (SRD items and other campaigns' items: 404).</summary>
public sealed class UpdateHomebrewItemHandler(
    ICampaignAccess access,
    IItemTemplateRepository templates,
    IValidator<ItemTemplateInput> validator,
    IUnitOfWork unitOfWork)
{
    public async Task<ItemDetailDto> HandleAsync(Guid currentUserId, Guid campaignId, Guid templateId, ItemTemplatePatch patch, CancellationToken cancellationToken = default)
    {
        await access.RequireAsync(campaignId, currentUserId, CampaignRole.DM, cancellationToken);
        var template = await templates.GetHomebrewAsync(campaignId, templateId, cancellationToken) ?? throw ItemErrors.ItemNotFound();

        var merged = patch.ApplyTo(ItemTemplateInput.From(template.ToData()));
        var validation = await validator.ValidateAsync(merged, cancellationToken);
        if (!validation.IsValid)
        {
            var error = validation.Errors[0];
            throw AppException.Validation(System.Text.Json.JsonNamingPolicy.CamelCase.ConvertName(error.PropertyName), error.ErrorMessage);
        }

        template.UpdateHomebrew(merged.ToData());
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ItemDetailDto.From(template);
    }
}

/// <summary>A DM deletes a homebrew item; 409 while an inventory entry or a shop item uses it.</summary>
public sealed class DeleteHomebrewItemHandler(ICampaignAccess access, IItemTemplateRepository templates, IUnitOfWork unitOfWork)
{
    public async Task HandleAsync(Guid currentUserId, Guid campaignId, Guid templateId, CancellationToken cancellationToken = default)
    {
        await access.RequireAsync(campaignId, currentUserId, CampaignRole.DM, cancellationToken);
        var template = await templates.GetHomebrewAsync(campaignId, templateId, cancellationToken) ?? throw ItemErrors.ItemNotFound();
        if (await templates.IsInUseAsync(template.Id, cancellationToken))
        {
            throw AppException.Conflict("El objeto está en el inventario de algún personaje o en una tienda.");
        }

        templates.Remove(template);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
