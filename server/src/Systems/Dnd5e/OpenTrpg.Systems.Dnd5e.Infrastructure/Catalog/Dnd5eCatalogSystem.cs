using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using OpenTrpg.Core.Application.Abstractions;
using OpenTrpg.Core.Application.ContentPacks;
using OpenTrpg.Core.Application.Systems;
using OpenTrpg.Core.Domain.Catalog;
using OpenTrpg.Core.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OpenTrpg.Core.Infrastructure;
using OpenTrpg.Core.Infrastructure.Catalog;
using OpenTrpg.Systems.Dnd5e.Application;
using OpenTrpg.Systems.Dnd5e.Application.Abstractions;
using OpenTrpg.Systems.Dnd5e.Domain.Catalog;
using OpenTrpg.Systems.Dnd5e.Infrastructure.Catalog;

namespace OpenTrpg.Systems.Dnd5e.Infrastructure.Catalog;

/// <summary>
/// The D&amp;D 5e catalog (<see cref="ICatalogSystem"/>): the SRD as base pack and the private content packs of the
/// instance (<c>docs/content-packs.md</c>). A pack is validated as a whole (every error with its JSON path) and its
/// definitions are written inside the transaction the core's <c>ContentPackRegistry</c> opened: the definitions with
/// <c>Source</c> = pack id are replaced and item templates are upserted by index so their ids survive (items dropped
/// from the pack are deleted unless an inventory, shop or stash uses them). Re-importing the same version replaces
/// the content as well.
/// </summary>
internal sealed partial class Dnd5eCatalogSystem(
    AppDbContext db,
    ISrdSeeder seeder,
    IDateTimeProvider clock,
    ILogger<Dnd5eCatalogSystem> logger) : IDnd5eCatalogSystem
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

    public IReadOnlyList<string> DefinitionTypes { get; } =
    [
        "classes", "classLevels", "subclasses", "subclassLevels", "features", "races", "subraces", "traits", "spells", "backgrounds",
        "items", "optionSets", "options", "levelChoiceRules", "trinkets", "rollTables", "creatures", "conditions", "rules", "reference",
    ];

    public bool IsReservedSource(string id) => Dnd5eCatalogSources.IsReserved(id);

    public async Task LoadBasePackAsync(CancellationToken cancellationToken = default) =>
        await seeder.SeedAsync(cancellationToken);

    public async Task<PackImportResult> ImportPackAsync(Stream json, CancellationToken cancellationToken = default)
    {
        var pack = await DeserializeAsync(json, cancellationToken);

        // The pack sees the SRD, the packs it requires and (to replace them) its own definitions.
        var reach = new HashSet<string>(StringComparer.Ordinal) { Dnd5eCatalogSources.Srd };
        reach.UnionWith((pack.Requires ?? []).OfType<string>().Select(r => r.Trim()));
        var ownId = pack.Id?.Trim() ?? string.Empty;
        reach.Add(ownId);
        var validator = new ContentPackValidator(await LoadContextAsync(reach, ownId, cancellationToken));
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

        // The pack's races are updated in place: other packs may have added subraces to them (races[].extends).
        var raceIndexes = rows.Races.Select(r => r.Index).ToList();
        // So are its classes: other packs may have added subclasses to them (classes[].extends).
        var classIndexes = rows.Classes.Select(c => c.Index).ToList();
        await DeleteDefinitionsAsync(id, raceIndexes, classIndexes, cancellationToken);
        var existingRaces = (await db.Set<RaceDefinition>().AsNoTracking().Where(x => x.Source == id).Select(x => x.Index).ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.Ordinal);
        var existingClasses = (await db.Set<ClassDefinition>().AsNoTracking().Where(x => x.Source == id).Select(x => x.Index).ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.Ordinal);

        db.Set<ClassDefinition>().UpdateRange(rows.Classes.Where(c => existingClasses.Contains(c.Index)));
        db.Set<ClassDefinition>().AddRange(rows.Classes.Where(c => !existingClasses.Contains(c.Index)));
        await db.SaveChangesAsync(cancellationToken);
        db.ChangeTracker.Clear();
        db.Set<ClassLevel>().AddRange(rows.ClassLevels);

        db.Set<SubclassDefinition>().AddRange(rows.Subclasses);
        db.Set<SubclassLevel>().AddRange(rows.SubclassLevels);
        db.Set<FeatureDefinition>().AddRange(rows.Features);
        db.Set<RaceDefinition>().UpdateRange(rows.Races.Where(r => existingRaces.Contains(r.Index)));
        db.Set<RaceDefinition>().AddRange(rows.Races.Where(r => !existingRaces.Contains(r.Index)));
        db.Set<SubraceDefinition>().AddRange(rows.Subraces);
        db.Set<RaceExtensionDefinition>().AddRange(rows.RaceExtensions);
        db.Set<TraitDefinition>().AddRange(rows.Traits);
        db.Set<SpellDefinition>().AddRange(rows.Spells);
        db.Set<BackgroundDefinition>().AddRange(rows.Backgrounds);
        db.Set<OptionSetDefinition>().AddRange(rows.OptionSets);
        db.Set<OptionDefinition>().AddRange(rows.Options);
        db.Set<LevelChoiceRule>().AddRange(rows.LevelChoiceRules);
        db.Set<TrinketEntry>().AddRange(rows.Trinkets);
        db.Set<RollTable>().AddRange(rows.RollTables);
        db.Set<CreatureDefinition>().AddRange(rows.Creatures);
        db.Set<ConditionDefinition>().AddRange(rows.Conditions);
        db.Set<RuleDefinition>().AddRange(rows.Rules);
        db.Set<ReferenceEntry>().AddRange(rows.ReferenceEntries);
        await db.SaveChangesAsync(cancellationToken);
        db.ChangeTracker.Clear();

        await CatalogItems.UpsertAsync(db, id, rows.Items, now, cancellationToken);
        await CatalogItems.DeleteUnusedAsync(db, id, rows.Items.Select(i => i.Index).ToList(), cancellationToken);
        return new PackImportResult(id, validator.Name, validator.Version, counts)
        {
            FormatVersion = ContentPackValidator.CurrentFormatVersion,
            Requires = validator.Requires,
        };
    }

    public async Task DeletePackAsync(string packId, CancellationToken cancellationToken = default)
    {
        await DeleteDefinitionsAsync(packId, [], [], cancellationToken);
        var kept = await CatalogItems.DeleteUnusedAsync(db, packId, [], cancellationToken);
        logger.LogInformation("Content pack {PackId}: {KeptItems} items kept because they are in use", packId, kept);
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

    /// <summary>
    /// What the pack can reference: the definitions of <paramref name="reach"/> (the SRD, the packs it requires and its
    /// own, which the validator tells apart by source). The pack's own classes are left out: it defines them again.
    /// </summary>
    private async Task<ContentPackContext> LoadContextAsync(IReadOnlySet<string> reachSet, string ownId, CancellationToken cancellationToken)
    {
        var reach = reachSet.ToList();
        var classes = await db.Set<ClassDefinition>().AsNoTracking()
            .Where(x => reach.Contains(x.Source) && x.Source != ownId)
            .Select(x => new { x.Index, x.SubclassFlavor })
            .ToDictionaryAsync(x => x.Index, x => x.SubclassFlavor, StringComparer.Ordinal, cancellationToken);
        var subclasses = await db.Set<SubclassDefinition>().AsNoTracking()
            .Where(x => reach.Contains(x.Source) && x.Source != ownId)
            .Select(x => new { x.Index, x.ClassIndex })
            .ToDictionaryAsync(x => x.Index, x => x.ClassIndex, StringComparer.Ordinal, cancellationToken);
        var packSubclasses = await db.Set<SubclassDefinition>().AsNoTracking()
            .Where(x => x.Source != Dnd5eCatalogSources.Srd)
            .Select(x => new { x.Index, x.ClassIndex, x.Source })
            .ToDictionaryAsync(x => x.Index, x => (x.ClassIndex, x.Source), StringComparer.Ordinal, cancellationToken);
        var skills = await db.Set<SkillDefinition>().AsNoTracking()
            .Select(x => new { x.Index, x.Name })
            .ToDictionaryAsync(x => x.Index, x => x.Name, StringComparer.Ordinal, cancellationToken);
        var optionSets = await db.Set<OptionSetDefinition>().AsNoTracking()
            .Where(x => reach.Contains(x.Source))
            .Select(x => new { x.SetId, x.Source })
            .ToDictionaryAsync(x => x.SetId, x => x.Source, StringComparer.Ordinal, cancellationToken);
        var options = await db.Set<OptionDefinition>().AsNoTracking()
            .Where(x => reach.Contains(x.Source))
            .Select(x => new { x.Index, x.SetId, x.Source })
            .ToDictionaryAsync(x => x.Index, x => (x.SetId, x.Source), StringComparer.Ordinal, cancellationToken);
        var spellRows = await db.Set<SpellDefinition>().AsNoTracking()
            .Where(x => reach.Contains(x.Source))
            .Select(x => new { x.Index, x.Source, x.Level })
            .ToListAsync(cancellationToken);
        var spells = spellRows.ToDictionary(x => x.Index, x => x.Source, StringComparer.Ordinal);
        var items = await db.ItemTemplates.AsNoTracking()
            .Where(x => x.CampaignId == null && reach.Contains(x.Source) && x.Source != ownId && x.Index != null)
            .Select(x => x.Index!)
            .ToListAsync(cancellationToken);
        var categories = await db.Set<EquipmentCategory>().AsNoTracking().Select(x => x.Index).ToListAsync(cancellationToken);
        var casterClasses = await db.Set<ClassDefinition>().AsNoTracking()
            .Where(x => reach.Contains(x.Source) && x.Source != ownId && (x.SpellcastingAbility != null || x.SpellcastingLevel > 0))
            .Select(x => x.Index)
            .ToListAsync(cancellationToken);
        var races = await db.Set<RaceDefinition>().AsNoTracking()
            .Where(x => reach.Contains(x.Source))
            .Select(x => new { x.Index, x.Source })
            .ToDictionaryAsync(x => x.Index, x => x.Source, StringComparer.Ordinal, cancellationToken);
        var resourceRows = await db.Set<OptionDefinition>().AsNoTracking()
            .Where(x => x.ResourceJson != null && reach.Contains(x.Source))
            .Select(x => new { x.ResourceJson, x.Source })
            .ToListAsync(cancellationToken);
        resourceRows.AddRange(await db.Set<FeatureDefinition>().AsNoTracking()
            .Where(x => x.ResourceJson != null && reach.Contains(x.Source))
            .Select(x => new { x.ResourceJson, x.Source })
            .ToListAsync(cancellationToken));
        var classResources = (await db.Set<ClassDefinition>().AsNoTracking()
                .Where(x => x.ResourcesJson != null && reach.Contains(x.Source))
                .Select(x => new { x.ResourcesJson, x.Source })
                .ToListAsync(cancellationToken))
            .SelectMany(x => LevelChoiceJson.ParseResources(x.ResourcesJson).Select(r => (r.Key, x.Source)))
            .ToList();
        var ruleRows = await db.Set<LevelChoiceRule>().AsNoTracking()
            .Where(x => reach.Contains(x.Source))
            .Select(x => new { x.ClassIndex, x.Level, x.Key, x.SetId, x.Source })
            .ToListAsync(cancellationToken);
        var packs = await db.ContentPacks.AsNoTracking()
            .Where(x => !x.IsBase)
            .Select(x => new { x.Id, x.SystemId })
            .ToDictionaryAsync(x => x.Id, x => x.SystemId, StringComparer.Ordinal, cancellationToken);
        var vocabularies = (await db.Set<ReferenceEntry>().AsNoTracking()
                .Where(x => reach.Contains(x.Source) && x.Source != ownId)
                .Select(x => new { x.Kind, x.Index })
                .ToListAsync(cancellationToken))
            .GroupBy(x => x.Kind, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => (IReadOnlySet<string>)g.Select(x => x.Index).ToHashSet(StringComparer.Ordinal), StringComparer.Ordinal);
        return new ContentPackContext(
            classes,
            subclasses,
            skills,
            optionSets,
            options,
            spells,
            items.ToHashSet(StringComparer.Ordinal),
            categories.ToHashSet(StringComparer.Ordinal),
            packSubclasses,
            races,
            casterClasses.ToHashSet(StringComparer.Ordinal),
            spellRows.ToDictionary(x => x.Index, x => x.Level, StringComparer.Ordinal),
            resourceRows.Select(x => (Key: LevelChoiceJson.ParseResource(x.ResourceJson)?.Key, x.Source))
                .Where(x => x.Key is not null)
                .Select(x => (x.Key!, x.Source))
                .Concat(classResources)
                .ToList(),
            ruleRows.Where(x => x.SetId != null).Select(x => (x.SetId!, x.ClassIndex, x.Source)).ToList(),
            ruleRows.Select(x => (x.ClassIndex, x.Level, x.Key, x.Source)).ToList())
        {
            Packs = packs,
            Vocabularies = vocabularies,
        };
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
            await db.Set<SubclassDefinition>().AsNoTracking().Where(x => x.Source != id && keys.Contains(x.Index)).Select(x => new { x.Index, x.Source }).ToListAsync(cancellationToken),
            x => (x.Index, x.Source)));
        await CheckAsync("subclassLevels", async keys => Pairs(
            await db.Set<SubclassLevel>().AsNoTracking().Where(x => x.Source != id && keys.Contains(x.Index)).Select(x => new { x.Index, x.Source }).ToListAsync(cancellationToken),
            x => (x.Index, x.Source)));
        await CheckAsync("features", async keys => Pairs(
            await db.Set<FeatureDefinition>().AsNoTracking().Where(x => x.Source != id && keys.Contains(x.Index)).Select(x => new { x.Index, x.Source }).ToListAsync(cancellationToken),
            x => (x.Index, x.Source)));
        await CheckAsync("spells", async keys => Pairs(
            await db.Set<SpellDefinition>().AsNoTracking().Where(x => x.Source != id && keys.Contains(x.Index)).Select(x => new { x.Index, x.Source }).ToListAsync(cancellationToken),
            x => (x.Index, x.Source)));
        await CheckAsync("races", async keys => Pairs(
            await db.Set<RaceDefinition>().AsNoTracking().Where(x => x.Source != id && keys.Contains(x.Index)).Select(x => new { x.Index, x.Source }).ToListAsync(cancellationToken),
            x => (x.Index, x.Source)));
        await CheckAsync("subraces", async keys => Pairs(
            await db.Set<SubraceDefinition>().AsNoTracking().Where(x => x.Source != id && keys.Contains(x.Index)).Select(x => new { x.Index, x.Source }).ToListAsync(cancellationToken),
            x => (x.Index, x.Source)));
        await CheckAsync("traits", async keys => Pairs(
            await db.Set<TraitDefinition>().AsNoTracking().Where(x => x.Source != id && keys.Contains(x.Index)).Select(x => new { x.Index, x.Source }).ToListAsync(cancellationToken),
            x => (x.Index, x.Source)));
        await CheckAsync("backgrounds", async keys => Pairs(
            await db.Set<BackgroundDefinition>().AsNoTracking().Where(x => x.Source != id && keys.Contains(x.Index)).Select(x => new { x.Index, x.Source }).ToListAsync(cancellationToken),
            x => (x.Index, x.Source)));
        await CheckAsync("items", async keys => Pairs(
            await db.ItemTemplates.AsNoTracking().Where(x => x.Source != id && x.Index != null && keys.Contains(x.Index)).Select(x => new { Index = x.Index!, x.Source }).ToListAsync(cancellationToken),
            x => (x.Index, x.Source)));

        await CheckAsync("optionSets", async keys => Pairs(
            await db.Set<OptionSetDefinition>().AsNoTracking().Where(x => x.Source != id && keys.Contains(x.SetId)).Select(x => new { x.SetId, x.Source }).ToListAsync(cancellationToken),
            x => (x.SetId, x.Source)));
        await CheckAsync("options", async keys => Pairs(
            await db.Set<OptionDefinition>().AsNoTracking().Where(x => x.Source != id && keys.Contains(x.Index)).Select(x => new { x.Index, x.Source }).ToListAsync(cancellationToken),
            x => (x.Index, x.Source)));
        await CheckAsync("levelChoiceRules", async keys => Pairs(
            await db.Set<LevelChoiceRule>().AsNoTracking().Where(x => x.Source != id && keys.Contains(x.Id)).Select(x => new { x.Id, x.Source }).ToListAsync(cancellationToken),
            x => (x.Id, x.Source)));

        await CheckAsync("classes", async keys => Pairs(
            await db.Set<ClassDefinition>().AsNoTracking().Where(x => x.Source != id && keys.Contains(x.Index)).Select(x => new { x.Index, x.Source }).ToListAsync(cancellationToken),
            x => (x.Index, x.Source)));
        await CheckAsync("creatures", async keys => Pairs(
            await db.Set<CreatureDefinition>().AsNoTracking().Where(x => x.Source != id && keys.Contains(x.Index)).Select(x => new { x.Index, x.Source }).ToListAsync(cancellationToken),
            x => (x.Index, x.Source)));
        await CheckAsync("conditions", async keys => Pairs(
            await db.Set<ConditionDefinition>().AsNoTracking().Where(x => x.Source != id && keys.Contains(x.Index)).Select(x => new { x.Index, x.Source }).ToListAsync(cancellationToken),
            x => (x.Index, x.Source)));
        await CheckAsync("rules", async keys => Pairs(
            await db.Set<RuleDefinition>().AsNoTracking().Where(x => x.Source != id && keys.Contains(x.Index)).Select(x => new { x.Index, x.Source }).ToListAsync(cancellationToken),
            x => (x.Index, x.Source)));
        foreach (var kind in ReferenceEntry.Kinds)
        {
            await CheckAsync($"reference.{kind}", async keys => Pairs(
                await db.Set<ReferenceEntry>().AsNoTracking().Where(x => x.Kind == kind && x.Source != id && keys.Contains(x.Index)).Select(x => new { x.Index, x.Source }).ToListAsync(cancellationToken),
                x => (x.Index, x.Source)));
        }

        // A subclass must not hide a class index either (nor a class a subclass).
        await CheckAsync("subclasses", async keys => Pairs(
            await db.Set<ClassDefinition>().AsNoTracking().Where(x => keys.Contains(x.Index)).Select(x => new { x.Index, x.Source }).ToListAsync(cancellationToken),
            x => (x.Index, x.Source)));
        await CheckAsync("classes", async keys => Pairs(
            await db.Set<SubclassDefinition>().AsNoTracking().Where(x => keys.Contains(x.Index)).Select(x => new { x.Index, x.Source }).ToListAsync(cancellationToken),
            x => (x.Index, x.Source)));
    }

    /// <summary>
    /// Deletes every definition of the pack except item templates (dependents first) and the races and classes in
    /// <paramref name="keptRaces"/> and <paramref name="keptClasses"/>, which the import updates in place (deleting a
    /// race or a class cascades over the subraces, extensions and subclasses other packs added to it).
    /// </summary>
    private async Task DeleteDefinitionsAsync(string id, IReadOnlyCollection<string> keptRaces, IReadOnlyCollection<string> keptClasses, CancellationToken cancellationToken)
    {
        await db.Set<CreatureDefinition>().Where(x => x.Source == id).ExecuteDeleteAsync(cancellationToken);
        await db.Set<ConditionDefinition>().Where(x => x.Source == id).ExecuteDeleteAsync(cancellationToken);
        await db.Set<RuleDefinition>().Where(x => x.Source == id).ExecuteDeleteAsync(cancellationToken);
        await db.Set<ReferenceEntry>().Where(x => x.Source == id).ExecuteDeleteAsync(cancellationToken);
        await db.Set<TrinketEntry>().Where(x => x.Source == id).ExecuteDeleteAsync(cancellationToken);
        await db.Set<RollTable>().Where(x => x.Source == id).ExecuteDeleteAsync(cancellationToken);
        await db.Set<LevelChoiceRule>().Where(x => x.Source == id).ExecuteDeleteAsync(cancellationToken);
        await db.Set<OptionDefinition>().Where(x => x.Source == id).ExecuteDeleteAsync(cancellationToken);
        await db.Set<OptionSetDefinition>().Where(x => x.Source == id).ExecuteDeleteAsync(cancellationToken);
        await db.Set<FeatureDefinition>().Where(x => x.Source == id).ExecuteDeleteAsync(cancellationToken);
        await db.Set<SubclassLevel>().Where(x => x.Source == id).ExecuteDeleteAsync(cancellationToken);
        await db.Set<SubclassDefinition>().Where(x => x.Source == id).ExecuteDeleteAsync(cancellationToken);
        await db.Set<SubraceDefinition>().Where(x => x.Source == id).ExecuteDeleteAsync(cancellationToken);
        await db.Set<RaceExtensionDefinition>().Where(x => x.Source == id).ExecuteDeleteAsync(cancellationToken);
        await db.Set<RaceDefinition>().Where(x => x.Source == id && !keptRaces.Contains(x.Index)).ExecuteDeleteAsync(cancellationToken);
        await db.Set<TraitDefinition>().Where(x => x.Source == id).ExecuteDeleteAsync(cancellationToken);
        await db.Set<SpellDefinition>().Where(x => x.Source == id).ExecuteDeleteAsync(cancellationToken);
        await db.Set<BackgroundDefinition>().Where(x => x.Source == id).ExecuteDeleteAsync(cancellationToken);
        await db.Set<ClassLevel>().Where(x => x.Source == id).ExecuteDeleteAsync(cancellationToken);
        await db.Set<ClassDefinition>().Where(x => x.Source == id && !keptClasses.Contains(x.Index)).ExecuteDeleteAsync(cancellationToken);
    }
}
