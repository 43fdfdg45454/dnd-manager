using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OpenTrpg.Core.Application.Abstractions;
using OpenTrpg.Core.Application.Common;
using OpenTrpg.Core.Application.ContentPacks;
using OpenTrpg.Core.Application.Systems;
using OpenTrpg.Core.Domain.Catalog;
using OpenTrpg.Core.Infrastructure.Persistence;

namespace OpenTrpg.Core.Infrastructure.Catalog;

/// <summary>
/// The registry of the content packs of the instance (<c>docs/content-packs.md</c>): one <see cref="ContentPack"/> per
/// pack. Importing and deleting run in one transaction in which the game system of the pack (the <c>"system"</c> field
/// of the JSON, or the default system of the instance; <see cref="ICatalogSystem"/>) validates it and writes or deletes
/// its definitions. A new pack is not enabled in any campaign; deleting a pack removes its activations.
/// </summary>
internal sealed class ContentPackRegistry(
    AppDbContext db,
    IGameSystemRegistry systems,
    IDateTimeProvider clock,
    ILogger<ContentPackRegistry> logger) : IContentPackImporter
{
    public const string BasePackCode = "base-pack";

    public async Task<ContentPackImportResultDto> ImportAsync(Stream json, CancellationToken cancellationToken = default)
    {
        var buffered = json as MemoryStream ?? await CopyAsync(json, cancellationToken);
        var system = SystemOf(buffered);
        buffered.Position = 0;

        PackImportResult result;
        var now = clock.UtcNow;
        await using (var transaction = await db.Database.BeginTransactionAsync(cancellationToken))
        {
            result = await system.Catalog.ImportPackAsync(buffered, cancellationToken);

            var existing = await db.ContentPacks.AsNoTracking().FirstOrDefaultAsync(x => x.Id == result.Id, cancellationToken);
            if (existing is { IsBase: true })
            {
                throw AppException.Conflict("No se puede reemplazar el paquete base del sistema.", BasePackCode);
            }

            if (existing is not null && existing.SystemId != system.Id)
            {
                throw new ContentPackInvalidException([$"id: El paquete '{result.Id}' ya existe para otro sistema ({existing.SystemId})."]);
            }

            var pack = new ContentPack
            {
                Id = result.Id,
                SystemId = system.Id,
                Name = result.Name,
                Version = result.Version,
                FormatVersion = result.FormatVersion,
                IsBase = false,
                ImportedAt = now,
                CountsJson = JsonSerializer.Serialize(result.Counts),
                Requires = result.Requires,
            };
            if (existing is null)
            {
                db.ContentPacks.Add(pack);
            }
            else
            {
                db.ContentPacks.Update(pack);
            }

            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        db.ChangeTracker.Clear();
        logger.LogInformation(
            "Content pack {PackId} {Version} imported: {Counts}",
            result.Id,
            result.Version,
            string.Join(", ", result.Counts.Select(c => $"{c.Key}={c.Value}")));
        return new ContentPackImportResultDto(result.Id, result.Name, result.Version, result.Counts)
        {
            SystemId = system.Id,
            FormatVersion = result.FormatVersion,
            Requires = result.Requires,
        };
    }

    public async Task<IReadOnlyList<ContentPackDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        var packs = await db.ContentPacks.AsNoTracking().ToListAsync(cancellationToken);
        return packs
            .Select(x => new ContentPackDto(x.Id, x.SystemId, x.Name, x.Version, x.FormatVersion, x.IsBase, x.ImportedAt, ParseCounts(x.CountsJson))
            {
                Requires = x.Requires,
            })
            .OrderByDescending(x => x.IsBase)
            .ThenBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(x => x.Id, StringComparer.Ordinal)
            .ToList();
    }

    public async Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        var pack = await db.ContentPacks.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (pack is null)
        {
            return false;
        }

        if (pack.IsBase || CatalogSources.IsReserved(id) || systems.All.Any(s => s.Catalog.IsReservedSource(id)))
        {
            throw AppException.Conflict("El paquete base del sistema no se puede borrar.", BasePackCode);
        }

        var system = systems.Find(pack.SystemId) ?? systems.Default;
        await using (var transaction = await db.Database.BeginTransactionAsync(cancellationToken))
        {
            await system.Catalog.DeletePackAsync(id, cancellationToken);
            await db.CampaignContentPacks.Where(x => x.PackId == id).ExecuteDeleteAsync(cancellationToken);
            await db.ContentPacks.Where(x => x.Id == id).ExecuteDeleteAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        logger.LogInformation("Content pack {PackId} deleted", id);
        return true;
    }

    /// <summary>The system named by the root <c>"system"</c> field (the default one when absent or unreadable).</summary>
    private IGameSystem SystemOf(MemoryStream json)
    {
        try
        {
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
            if (document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("system", out var value)
                && value.ValueKind == JsonValueKind.String
                && value.GetString() is { Length: > 0 } id)
            {
                return systems.Find(id) ?? throw new ContentPackInvalidException([$"system: El sistema '{id}' no está instalado en esta instancia."]);
            }
        }
        catch (JsonException)
        {
            // The system reports the JSON error with its position.
        }

        return systems.Default;
    }

    private static async Task<MemoryStream> CopyAsync(Stream json, CancellationToken cancellationToken)
    {
        var buffer = new MemoryStream();
        await json.CopyToAsync(buffer, cancellationToken);
        buffer.Position = 0;
        return buffer;
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
