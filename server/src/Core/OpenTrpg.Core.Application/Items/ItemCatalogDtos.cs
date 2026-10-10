using OpenTrpg.Core.Domain.Catalog;

namespace OpenTrpg.Core.Application.Items;

/// <summary>Origin of an item template as shown by the API.</summary>
public static class ItemSources
{
    public const string Homebrew = CatalogSources.Homebrew;

    /// <summary>"srd", "homebrew" or the id of the content pack.</summary>
    public static string Of(ItemTemplate item) => item.IsSrd ? item.Source : Homebrew;
}

/// <param name="Source">"srd", "homebrew" (campaign item) or the id of the content pack.</param>
public sealed record ItemSummaryDto(
    Guid Id,
    string? Index,
    string Name,
    string Category,
    string Subcategory,
    string? Rarity,
    bool RequiresAttunement,
    int? CostCp,
    decimal? WeightLb,
    string Source)
{
    public static ItemSummaryDto From(ItemTemplate i) => new(
        i.Id, i.Index, i.Name, i.Category.ToString(), i.Subcategory, i.Rarity?.ToString(), i.RequiresAttunement, i.Cost, i.WeightLb, ItemSources.Of(i));
}

public sealed record ItemDetailDto(
    Guid Id,
    Guid? CampaignId,
    string? Index,
    string Name,
    string Category,
    string Subcategory,
    string? Rarity,
    bool RequiresAttunement,
    int? CostCp,
    decimal? WeightLb,
    string? DamageDice,
    string? DamageType,
    string? VersatileDice,
    IReadOnlyList<string> Properties,
    int? RangeNormal,
    int? RangeLong,
    int? ArmorClassBase,
    bool? AddDexModifier,
    int? MaxDexBonus,
    int? StrengthMinimum,
    bool StealthDisadvantage,
    IReadOnlyList<string> Description,
    bool IsSrd,
    DateTimeOffset CreatedAt,
    IReadOnlyList<string> Effects,
    string Source,
    IReadOnlyList<ItemModifierDto> Modifiers)
{
    /// <summary>System-specific rules data (D&amp;D 5e: firearm reload and misfire, tool, ammunition...), or null.</summary>
    public System.Text.Json.JsonElement? SystemData { get; init; }

    public static ItemDetailDto From(ItemTemplate i) => new(
        i.Id, i.CampaignId, i.Index, i.Name, i.Category.ToString(), i.Subcategory, i.Rarity?.ToString(), i.RequiresAttunement,
        i.Cost, i.WeightLb, i.DamageDice, i.DamageType, i.VersatileDice, i.Properties, i.RangeNormal, i.RangeLong,
        i.ArmorClassBase, i.AddDexModifier, i.MaxDexBonus, i.StrengthMinimum, i.StealthDisadvantage, i.Description, i.IsSrd, i.CreatedAt,
        i.Effects, ItemSources.Of(i), ItemModifierDto.FromAll(i.Modifiers))
    {
        SystemData = i.SystemDataJson is { } json ? System.Text.Json.JsonDocument.Parse(json).RootElement.Clone() : null,
    };
}
