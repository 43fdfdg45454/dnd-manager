using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OpenTrpg.Core.Application.ContentPacks;
using OpenTrpg.Core.Infrastructure.Persistence;
using OpenTrpg.Systems.Dnd5e.Application.Catalog;
using OpenTrpg.Systems.Dnd5e.Domain.Catalog;

namespace OpenTrpg.Systems.Dnd5e.Infrastructure.Catalog;

/// <summary>
/// <see cref="IBeastCatalog"/> over the table <c>Dnd5eCreatures</c> (the SRD beasts of the base pack and the creatures of
/// content packs). The list follows the catalog scope of the request; a lookup by index does not, so a companion keeps
/// its statblock when its pack is disabled. Loaded once per request scope.
/// </summary>
internal sealed class SrdBeastCatalog(AppDbContext db, CatalogScopeContext scope) : IBeastCatalog
{
    internal static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private List<BeastDto>? _all;

    public async Task<IReadOnlyList<BeastDto>> ListAsync(CancellationToken cancellationToken = default) =>
        (await AllAsync(cancellationToken)).Where(b => scope.Allows(b.Source)).ToList();

    public async Task<BeastDto?> FindAsync(string index, CancellationToken cancellationToken = default) =>
        (await AllAsync(cancellationToken)).FirstOrDefault(b => string.Equals(b.Index, index, StringComparison.OrdinalIgnoreCase));

    /// <summary>The statblock of a row (the searchable columns win over the JSON).</summary>
    public static BeastDto? ToDto(CreatureDefinition row)
    {
        try
        {
            return JsonSerializer.Deserialize<BeastDto>(row.DataJson, JsonOptions) is { } beast
                ? beast with { Index = row.Index, Name = row.Name, Type = row.Type, Subtype = row.Subtype, Source = row.Source }
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>The row of a statblock.</summary>
    public static CreatureDefinition ToRow(BeastDto beast, string source) => new()
    {
        Index = beast.Index,
        Name = beast.Name,
        Type = beast.Type,
        Subtype = beast.Subtype,
        Size = beast.Size,
        ChallengeRating = beast.ChallengeRating,
        DataJson = JsonSerializer.Serialize(beast with { Source = source }, JsonOptions),
        Source = source,
    };

    private async Task<List<BeastDto>> AllAsync(CancellationToken cancellationToken)
    {
        if (_all is null)
        {
            var rows = await db.Set<CreatureDefinition>().AsNoTracking().ToListAsync(cancellationToken);
            _all = rows.Select(ToDto).OfType<BeastDto>().OrderBy(b => b.Name, StringComparer.OrdinalIgnoreCase).ToList();
        }

        return _all;
    }
}
