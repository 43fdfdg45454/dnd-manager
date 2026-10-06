using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Dnd.Application.Abstractions;
using Dnd.Application.ContentPacks;
using Dnd.Domain.Catalog;
using Dnd.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Dnd.Infrastructure.Catalog;

/// <summary>
/// Imports the private content packs of the instance (<c>docs/content-packs.md</c>). A pack is validated as a
/// whole (every error with its JSON path) and then written in one transaction: the definitions with
/// <c>Source</c> = pack id are replaced, item templates are upserted by index so their ids survive (items
/// dropped from the pack are deleted unless an inventory, shop or stash uses them) and the
/// <see cref="CatalogImport"/> of the pack (<c>pack:&lt;id&gt;</c>) is rewritten. Re-importing the same
/// version replaces the content as well.
/// </summary>
internal sealed partial class ContentPackImporter(AppDbContext db, IDateTimeProvider clock, ILogger<ContentPackImporter> logger) : IContentPackImporter
{
    /// <summary>Errors returned at most; the rest are summarized in one line.</summary>
    public const int MaxErrors = 100;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        MaxDepth = 32,
    };

    public async Task<ContentPackImportResultDto> ImportAsync(Stream json, CancellationToken cancellationToken = default)
    {
        var pack = await DeserializeAsync(json, cancellationToken);
        var validator = new ContentPackValidator(await LoadContextAsync(cancellationToken));
        var rows = validator.Validate(pack);
        if (validator.Id.Length > 0)
        {
            await CheckCollisionsAsync(validator, cancellationToken);
        }

        if (validator.Errors.Count > 0)
        {
            throw Invalid(validator.Errors);
        }

        var id = validator.Id;
        var now = clock.UtcNow;
        var counts = rows.Counts();

        await using (var transaction = await db.Database.BeginTransactionAsync(cancellationToken))
        {
            await DeleteDefinitionsAsync(id, cancellationToken);

            db.CatalogSubclasses.AddRange(rows.Subclasses);
            db.CatalogSubclassLevels.AddRange(rows.SubclassLevels);
            db.CatalogFeatures.AddRange(rows.Features);
            db.CatalogRaces.AddRange(rows.Races);
            db.CatalogSubraces.AddRange(rows.Subraces);
            db.CatalogTraits.AddRange(rows.Traits);
            db.CatalogSpells.AddRange(rows.Spells);
            db.CatalogBackgrounds.AddRange(rows.Backgrounds);
            db.CatalogOptionSets.AddRange(rows.OptionSets);
            db.CatalogOptions.AddRange(rows.Options);
            db.CatalogLevelChoiceRules.AddRange(rows.LevelChoiceRules);
            await db.SaveChangesAsync(cancellationToken);
            db.ChangeTracker.Clear();

            await CatalogItems.UpsertAsync(db, id, rows.Items, now, cancellationToken);
            await CatalogItems.DeleteUnusedAsync(db, id, rows.Items.Select(i => i.Index).ToList(), cancellationToken);

            var ruleset = CatalogSources.PackRuleset(id);
            await db.CatalogImports.Where(x => x.Ruleset == ruleset).ExecuteDeleteAsync(cancellationToken);
            db.CatalogImports.Add(new CatalogImport
            {
                Ruleset = ruleset,
                DatasetVersion = validator.Version,
                Name = validator.Name,
                ImportedAt = now,
                CreatedAt = now,
                CountsJson = JsonSerializer.Serialize(counts),
            });
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        db.ChangeTracker.Clear();
        logger.LogInformation(
            "Content pack {PackId} {Version} imported: {Counts}",
            id,
            validator.Version,
            string.Join(", ", counts.Select(c => $"{c.Key}={c.Value}")));
        return new ContentPackImportResultDto(id, validator.Name, validator.Version, counts);
    }

    public async Task<IReadOnlyList<ContentPackDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        var imports = await db.CatalogImports.AsNoTracking()
            .Where(x => x.Ruleset.StartsWith(CatalogSources.PackRulesetPrefix))
            .ToListAsync(cancellationToken);

        return imports
            .Select(x => new ContentPackDto(
                x.Ruleset[CatalogSources.PackRulesetPrefix.Length..],
                x.Name ?? x.Ruleset[CatalogSources.PackRulesetPrefix.Length..],
                x.DatasetVersion,
                x.ImportedAt,
                ParseCounts(x.CountsJson)))
            .OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(x => x.Id, StringComparer.Ordinal)
            .ToList();
    }

    public async Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        if (CatalogSources.IsReserved(id))
        {
            return false;
        }

        var ruleset = CatalogSources.PackRuleset(id);
        if (!await db.CatalogImports.AnyAsync(x => x.Ruleset == ruleset, cancellationToken))
        {
            return false;
        }

        await using (var transaction = await db.Database.BeginTransactionAsync(cancellationToken))
        {
            await DeleteDefinitionsAsync(id, cancellationToken);
            var kept = await CatalogItems.DeleteUnusedAsync(db, id, [], cancellationToken);
            await db.CatalogImports.Where(x => x.Ruleset == ruleset).ExecuteDeleteAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            logger.LogInformation("Content pack {PackId} deleted ({KeptItems} items kept because they are in use)", id, kept);
        }

        return true;
    }

    private static ContentPackInvalidException Invalid(IReadOnlyList<string> errors) =>
        new(errors.Count <= MaxErrors
            ? errors
            : [.. errors.Take(MaxErrors), $"… y {errors.Count - MaxErrors} errores más."]);

    private static async Task<PackJson> DeserializeAsync(Stream json, CancellationToken cancellationToken)
    {
        try
        {
            return await JsonSerializer.DeserializeAsync<PackJson>(json, JsonOptions, cancellationToken)
                ?? throw Invalid(["$: El paquete debe ser un objeto JSON."]);
        }
        catch (JsonException exception)
        {
            throw Invalid([DescribeJsonError(exception)]);
        }
    }

    /// <summary>"path: message" for a deserialization error (unknown property, wrong type or invalid JSON).</summary>
    private static string DescribeJsonError(JsonException exception)
    {
        var path = exception.Path is { Length: > 0 } p ? p.TrimStart('$').TrimStart('.') : string.Empty;
        if (exception.Message.Contains("could not be mapped", StringComparison.Ordinal)
            && UnmappedPropertyPattern().Match(exception.Message) is { Success: true } match)
        {
            var property = match.Groups[1].Value;
            var propertyPath = path.Length == 0 ? property
                : path == property || path.EndsWith($".{property}", StringComparison.Ordinal) ? path
                : $"{path}.{property}";
            return $"{propertyPath}: Propiedad desconocida (revisa el nombre).";
        }

        if (exception.LineNumber is { } line)
        {
            var location = $"línea {line + 1}, posición {(exception.BytePositionInLine ?? 0) + 1}";
            return exception.InnerException is null && exception.Message.Contains("could not be converted", StringComparison.Ordinal)
                ? $"{(path.Length == 0 ? "$" : path)}: Tipo de valor no válido ({location})."
                : $"{(path.Length == 0 ? "$" : path)}: JSON no válido ({location}).";
        }

        return $"{(path.Length == 0 ? "$" : path)}: JSON no válido.";
    }

    [GeneratedRegex("JSON property '([^']+)'")]
    private static partial Regex UnmappedPropertyPattern();

    private async Task<ContentPackContext> LoadContextAsync(CancellationToken cancellationToken)
    {
        var classes = await db.CatalogClasses.AsNoTracking()
            .Select(x => new { x.Index, x.SubclassFlavor })
            .ToDictionaryAsync(x => x.Index, x => x.SubclassFlavor, StringComparer.Ordinal, cancellationToken);
        var subclasses = await db.CatalogSubclasses.AsNoTracking()
            .Where(x => x.Source == CatalogSources.Srd)
            .Select(x => new { x.Index, x.ClassIndex })
            .ToDictionaryAsync(x => x.Index, x => x.ClassIndex, StringComparer.Ordinal, cancellationToken);
        var skills = await db.CatalogSkills.AsNoTracking()
            .Select(x => new { x.Index, x.Name })
            .ToDictionaryAsync(x => x.Index, x => x.Name, StringComparer.Ordinal, cancellationToken);
        var optionSets = await db.CatalogOptionSets.AsNoTracking()
            .Select(x => new { x.SetId, x.Source })
            .ToDictionaryAsync(x => x.SetId, x => x.Source, StringComparer.Ordinal, cancellationToken);
        var options = await db.CatalogOptions.AsNoTracking()
            .Select(x => new { x.Index, x.SetId, x.Source })
            .ToDictionaryAsync(x => x.Index, x => (x.SetId, x.Source), StringComparer.Ordinal, cancellationToken);
        var spells = await db.CatalogSpells.AsNoTracking()
            .Select(x => new { x.Index, x.Source })
            .ToDictionaryAsync(x => x.Index, x => x.Source, StringComparer.Ordinal, cancellationToken);
        var srdItems = await db.ItemTemplates.AsNoTracking()
            .Where(x => x.CampaignId == null && x.Source == CatalogSources.Srd && x.Index != null)
            .Select(x => x.Index!)
            .ToListAsync(cancellationToken);
        var categories = await db.CatalogEquipmentCategories.AsNoTracking().Select(x => x.Index).ToListAsync(cancellationToken);
        return new ContentPackContext(
            classes,
            subclasses,
            skills,
            optionSets,
            options,
            spells,
            srdItems.ToHashSet(StringComparer.Ordinal),
            categories.ToHashSet(StringComparer.Ordinal));
    }

    /// <summary>Reports the indexes of the pack already used by another source (the SRD or another pack).</summary>
    private async Task CheckCollisionsAsync(ContentPackValidator validator, CancellationToken cancellationToken)
    {
        var id = validator.Id;

        async Task CheckAsync(string table, Func<List<string>, Task<List<(string Index, string Source)>>> query)
        {
            if (!validator.IndexPaths.TryGetValue(table, out var paths) || paths.Count == 0)
            {
                return;
            }

            foreach (var (index, source) in await query(paths.Keys.ToList()))
            {
                validator.AddError(paths[index], $"El índice '{index}' ya existe en otra fuente ({source}).");
            }
        }

        static List<(string, string)> Pairs<T>(IEnumerable<T> rows, Func<T, (string, string)> map) => rows.Select(map).ToList();

        await CheckAsync("subclasses", async keys => Pairs(
            await db.CatalogSubclasses.AsNoTracking().Where(x => x.Source != id && keys.Contains(x.Index)).Select(x => new { x.Index, x.Source }).ToListAsync(cancellationToken),
            x => (x.Index, x.Source)));
        await CheckAsync("subclassLevels", async keys => Pairs(
            await db.CatalogSubclassLevels.AsNoTracking().Where(x => x.Source != id && keys.Contains(x.Index)).Select(x => new { x.Index, x.Source }).ToListAsync(cancellationToken),
            x => (x.Index, x.Source)));
        await CheckAsync("features", async keys => Pairs(
            await db.CatalogFeatures.AsNoTracking().Where(x => x.Source != id && keys.Contains(x.Index)).Select(x => new { x.Index, x.Source }).ToListAsync(cancellationToken),
            x => (x.Index, x.Source)));
        await CheckAsync("spells", async keys => Pairs(
            await db.CatalogSpells.AsNoTracking().Where(x => x.Source != id && keys.Contains(x.Index)).Select(x => new { x.Index, x.Source }).ToListAsync(cancellationToken),
            x => (x.Index, x.Source)));
        await CheckAsync("races", async keys => Pairs(
            await db.CatalogRaces.AsNoTracking().Where(x => x.Source != id && keys.Contains(x.Index)).Select(x => new { x.Index, x.Source }).ToListAsync(cancellationToken),
            x => (x.Index, x.Source)));
        await CheckAsync("subraces", async keys => Pairs(
            await db.CatalogSubraces.AsNoTracking().Where(x => x.Source != id && keys.Contains(x.Index)).Select(x => new { x.Index, x.Source }).ToListAsync(cancellationToken),
            x => (x.Index, x.Source)));
        await CheckAsync("traits", async keys => Pairs(
            await db.CatalogTraits.AsNoTracking().Where(x => x.Source != id && keys.Contains(x.Index)).Select(x => new { x.Index, x.Source }).ToListAsync(cancellationToken),
            x => (x.Index, x.Source)));
        await CheckAsync("backgrounds", async keys => Pairs(
            await db.CatalogBackgrounds.AsNoTracking().Where(x => x.Source != id && keys.Contains(x.Index)).Select(x => new { x.Index, x.Source }).ToListAsync(cancellationToken),
            x => (x.Index, x.Source)));
        await CheckAsync("items", async keys => Pairs(
            await db.ItemTemplates.AsNoTracking().Where(x => x.Source != id && x.Index != null && keys.Contains(x.Index)).Select(x => new { Index = x.Index!, x.Source }).ToListAsync(cancellationToken),
            x => (x.Index, x.Source)));

        await CheckAsync("optionSets", async keys => Pairs(
            await db.CatalogOptionSets.AsNoTracking().Where(x => x.Source != id && keys.Contains(x.SetId)).Select(x => new { x.SetId, x.Source }).ToListAsync(cancellationToken),
            x => (x.SetId, x.Source)));
        await CheckAsync("options", async keys => Pairs(
            await db.CatalogOptions.AsNoTracking().Where(x => x.Source != id && keys.Contains(x.Index)).Select(x => new { x.Index, x.Source }).ToListAsync(cancellationToken),
            x => (x.Index, x.Source)));
        await CheckAsync("levelChoiceRules", async keys => Pairs(
            await db.CatalogLevelChoiceRules.AsNoTracking().Where(x => x.Source != id && keys.Contains(x.Id)).Select(x => new { x.Id, x.Source }).ToListAsync(cancellationToken),
            x => (x.Id, x.Source)));

        // Classes are not extended by packs, but a subclass or feature must not hide a class index either.
        await CheckAsync("subclasses", async keys => Pairs(
            await db.CatalogClasses.AsNoTracking().Where(x => keys.Contains(x.Index)).Select(x => x.Index).ToListAsync(cancellationToken),
            x => (x, CatalogSources.Srd)));
    }

    /// <summary>Deletes every definition of the pack except item templates (dependents first).</summary>
    private async Task DeleteDefinitionsAsync(string id, CancellationToken cancellationToken)
    {
        await db.CatalogLevelChoiceRules.Where(x => x.Source == id).ExecuteDeleteAsync(cancellationToken);
        await db.CatalogOptions.Where(x => x.Source == id).ExecuteDeleteAsync(cancellationToken);
        await db.CatalogOptionSets.Where(x => x.Source == id).ExecuteDeleteAsync(cancellationToken);
        await db.CatalogFeatures.Where(x => x.Source == id).ExecuteDeleteAsync(cancellationToken);
        await db.CatalogSubclassLevels.Where(x => x.Source == id).ExecuteDeleteAsync(cancellationToken);
        await db.CatalogSubclasses.Where(x => x.Source == id).ExecuteDeleteAsync(cancellationToken);
        await db.CatalogSubraces.Where(x => x.Source == id).ExecuteDeleteAsync(cancellationToken);
        await db.CatalogRaces.Where(x => x.Source == id).ExecuteDeleteAsync(cancellationToken);
        await db.CatalogTraits.Where(x => x.Source == id).ExecuteDeleteAsync(cancellationToken);
        await db.CatalogSpells.Where(x => x.Source == id).ExecuteDeleteAsync(cancellationToken);
        await db.CatalogBackgrounds.Where(x => x.Source == id).ExecuteDeleteAsync(cancellationToken);
    }

    private static IReadOnlyDictionary<string, int> ParseCounts(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, int>>(json) ?? [];
        }
        catch (JsonException)
        {
            return new Dictionary<string, int>();
        }
    }
}
