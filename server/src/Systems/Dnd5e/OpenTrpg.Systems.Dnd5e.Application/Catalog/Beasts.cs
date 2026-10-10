using System.Globalization;
using FluentValidation;
using OpenTrpg.Core.Application.Common;
using OpenTrpg.Core.Application;
using OpenTrpg.Systems.Dnd5e.Application;

namespace OpenTrpg.Systems.Dnd5e.Application.Catalog;

// ---- DTOs ----------------------------------------------------------------------------------------

/// <summary>A beast in a list: enough to choose a wild shape.</summary>
/// <param name="ChallengeRating">Challenge rating as a number (0.125, 0.25, 0.5, 1...).</param>
/// <param name="ChallengeRatingText">Challenge rating as printed ("1/8", "1/4", "1/2", "1"...).</param>
/// <param name="Speeds">Speeds in feet by kind ("walk", "swim", "fly", "climb", "burrow").</param>
public sealed record BeastSummaryDto(
    string Index,
    string Name,
    string Size,
    double ChallengeRating,
    string ChallengeRatingText,
    int ArmorClass,
    int HitPoints,
    IReadOnlyDictionary<string, int> Speeds)
{
    /// <summary>Creature type ("beast", "monstrosity"...).</summary>
    public string Type { get; init; } = BeastDto.BeastType;

    /// <summary>"srd" or the content pack of the creature.</summary>
    public string Source { get; init; } = "srd";
}

/// <summary>One damage component of a beast action ("1d6+3" piercing).</summary>
public sealed record BeastDamageDto(string Dice, string? Type);

/// <summary>A saving throw the action or trait asks for (DC and ability index).</summary>
public sealed record BeastSaveDto(int Dc, string Ability);

/// <summary>A trait (special ability) of a beast.</summary>
public sealed record BeastTraitDto(string Name, string Description, BeastSaveDto? Save);

/// <summary>An action of a beast; attacks carry their bonus and damage dice.</summary>
public sealed record BeastActionDto(
    string Name,
    string Description,
    int? AttackBonus,
    IReadOnlyList<BeastDamageDto> Damage,
    BeastSaveDto? Save,
    bool IsMultiattack);

/// <summary>Full statblock of a creature of the catalog (the SRD beasts and the creatures of content packs).</summary>
/// <param name="Abilities">Scores by ability index ("str".."cha").</param>
/// <param name="SavingThrows">Saving throw bonuses by ability index, only the proficient ones.</param>
/// <param name="Skills">Skill bonuses by skill index ("perception"), only the proficient ones.</param>
/// <param name="Senses">Special senses ("darkvision" → "60 ft."), without the passive Perception.</param>
public sealed record BeastDto(
    string Index,
    string Name,
    string Size,
    string Alignment,
    double ChallengeRating,
    string ChallengeRatingText,
    int Xp,
    int ProficiencyBonus,
    int ArmorClass,
    string? ArmorClassType,
    int HitPoints,
    string HitDice,
    string? HitPointsRoll,
    IReadOnlyDictionary<string, int> Speeds,
    IReadOnlyDictionary<string, int> Abilities,
    IReadOnlyDictionary<string, int> SavingThrows,
    IReadOnlyDictionary<string, int> Skills,
    IReadOnlyDictionary<string, string> Senses,
    int PassivePerception,
    string Languages,
    IReadOnlyList<string> DamageVulnerabilities,
    IReadOnlyList<string> DamageResistances,
    IReadOnlyList<string> DamageImmunities,
    IReadOnlyList<string> ConditionImmunities,
    IReadOnlyList<BeastTraitDto> Traits,
    IReadOnlyList<BeastActionDto> Actions,
    string? Description)
{
    public const string BeastType = "beast";

    /// <summary>Creature type in lowercase ("beast", "monstrosity", "humanoid"...).</summary>
    public string Type { get; init; } = BeastType;

    public string? Subtype { get; init; }

    public IReadOnlyList<BeastActionDto> Reactions { get; init; } = [];

    public IReadOnlyList<BeastActionDto> LegendaryActions { get; init; } = [];

    /// <summary>"srd" or the content pack of the creature.</summary>
    public string Source { get; init; } = "srd";

    public bool IsBeast => string.Equals(Type, BeastType, StringComparison.OrdinalIgnoreCase);

    public BeastSummaryDto ToSummary() =>
        new(Index, Name, Size, ChallengeRating, ChallengeRatingText, ArmorClass, HitPoints, Speeds) { Type = Type, Source = Source };

    /// <summary>Printed form of a challenge rating: fractions below 1 ("1/4"), integers otherwise.</summary>
    public static string FormatChallengeRating(double cr) => cr switch
    {
        0.125 => "1/8",
        0.25 => "1/4",
        0.5 => "1/2",
        _ => cr.ToString("0.###", CultureInfo.InvariantCulture),
    };
}

/// <summary>The creatures of the catalog (table <c>Dnd5eCreatures</c>: the SRD beasts and the creatures of content packs).</summary>
public interface IBeastCatalog
{
    /// <summary>The creatures of the current catalog scope, ordered by name.</summary>
    Task<IReadOnlyList<BeastDto>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>A creature by index whatever its source (a companion keeps its statblock if its pack is disabled), or null.</summary>
    Task<BeastDto?> FindAsync(string index, CancellationToken cancellationToken = default);
}

// ---- Queries -------------------------------------------------------------------------------------

/// <param name="MaxCr">Highest challenge rating (0.25 for 1/4); null for any.</param>
/// <param name="Fly">true: only beasts with a flying speed; false: only beasts without one; null: any.</param>
/// <param name="Swim">true: only beasts with a swimming speed; false: only beasts without one; null: any.</param>
/// <param name="Q">Term matched against the name (case-insensitive).</param>
/// <param name="Type">Creature type ("beast"); null for any.</param>
public sealed record SearchBeastsQuery(double? MaxCr, bool? Fly, bool? Swim, string? Q, string? Type = null);

public sealed class SearchBeastsQueryValidator : AbstractValidator<SearchBeastsQuery>
{
    public SearchBeastsQueryValidator()
    {
        RuleFor(x => x.MaxCr).InclusiveBetween(0, 30).WithMessage("El VD máximo debe estar entre 0 y 30.");
        RuleFor(x => x.Q).MaximumLength(CatalogQueryDefaults.SearchMaxLength).WithMessage("La búsqueda es demasiado larga.");
    }
}

/// <summary>Creatures matching the filters, ordered by challenge rating and name.</summary>
public sealed class SearchBeastsHandler(IBeastCatalog beasts)
{
    public async Task<IReadOnlyList<BeastSummaryDto>> HandleAsync(SearchBeastsQuery query, CancellationToken cancellationToken = default)
    {
        var search = CatalogQueryDefaults.NormalizeSearch(query.Q);
        var type = string.IsNullOrWhiteSpace(query.Type) ? null : query.Type.Trim();
        return (await beasts.ListAsync(cancellationToken))
            .Where(b => type is null || string.Equals(b.Type, type, StringComparison.OrdinalIgnoreCase))
            .Where(b => query.MaxCr is not { } max || b.ChallengeRating <= max)
            .Where(b => query.Fly is not { } fly || HasSpeed(b, "fly") == fly)
            .Where(b => query.Swim is not { } swim || HasSpeed(b, "swim") == swim)
            .Where(b => search is null || b.Name.Contains(search, StringComparison.OrdinalIgnoreCase))
            .OrderBy(b => b.ChallengeRating)
            .ThenBy(b => b.Name, StringComparer.OrdinalIgnoreCase)
            .Select(b => b.ToSummary())
            .ToList();
    }

    private static bool HasSpeed(BeastDto beast, string kind) => beast.Speeds.GetValueOrDefault(kind) > 0;
}

/// <summary>Statblock of a creature by index (404 when unknown).</summary>
public sealed class GetBeastHandler(IBeastCatalog beasts)
{
    public async Task<BeastDto> HandleAsync(string index, CancellationToken cancellationToken = default) =>
        await beasts.FindAsync(index.Trim().ToLowerInvariant(), cancellationToken) ?? throw CatalogErrors.BeastNotFound();
}
