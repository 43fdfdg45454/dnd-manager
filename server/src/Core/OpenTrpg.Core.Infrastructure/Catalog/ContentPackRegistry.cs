using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OpenTrpg.Core.Application.Abstractions;
using OpenTrpg.Core.Application.ContentPacks;
using OpenTrpg.Core.Application.Systems;
using OpenTrpg.Core.Domain.Catalog;
using OpenTrpg.Core.Infrastructure.Persistence;

namespace OpenTrpg.Core.Infrastructure.Catalog;

/// <summary>
/// The registry of the content packs of the instance (<c>docs/content-packs.md</c>): one <see cref="CatalogImport"/> per
/// pack (<c>pack:&lt;id&gt;</c>). Importing and deleting run in one transaction in which the game system of the pack
/// (<see cref="ICatalogSystem"/>) validates it and writes or deletes its definitions. Until packs declare their system,
/// they belong to the default system of the instance.
/// </summary>
internal sealed class ContentPackRegistry(
    AppDbContext db,
    IGameSystemRegistry systems,
    IDateTimeProvider clock,
    ILogger<ContentPackRegistry> logger) : IContentPackImporter
{
    public async Task<ContentPackImportResultDto> ImportAsync(Stream json, CancellationToken cancellationToken = default)
    {
        var system = systems.Default;
        PackImportResult result;
        var now = clock.UtcNow;

        await using (var transaction = await db.Database.BeginTransactionAsync(cancellationToken))
        {
            result = await system.Catalog.ImportPackAsync(json, cancellationToken);

            var ruleset = CatalogSources.PackRuleset(result.Id);
            await db.CatalogImports.Where(x => x.Ruleset == ruleset).ExecuteDeleteAsync(cancellationToken);
            db.CatalogImports.Add(new CatalogImport
            {
                Ruleset = ruleset,
                DatasetVersion = result.Version,
                Name = result.Name,
                ImportedAt = now,
                CreatedAt = now,
                CountsJson = JsonSerializer.Serialize(result.Counts),
            });
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        db.ChangeTracker.Clear();
        logger.LogInformation(
            "Content pack {PackId} {Version} imported: {Counts}",
            result.Id,
            result.Version,
            string.Join(", ", result.Counts.Select(c => $"{c.Key}={c.Value}")));
        return new ContentPackImportResultDto(result.Id, result.Name, result.Version, result.Counts);
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
        if (CatalogSources.IsReserved(id) || systems.All.Any(s => s.Catalog.IsReservedSource(id)))
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
            await systems.Default.Catalog.DeletePackAsync(id, cancellationToken);
            await db.CatalogImports.Where(x => x.Ruleset == ruleset).ExecuteDeleteAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        logger.LogInformation("Content pack {PackId} deleted", id);
        return true;
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
