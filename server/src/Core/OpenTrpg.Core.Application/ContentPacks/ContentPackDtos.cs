namespace OpenTrpg.Core.Application.ContentPacks;

/// <summary>An imported content pack. <see cref="Counts"/>: definitions per type ("subclasses", "features", "items"...).</summary>
public sealed record ContentPackDto(string Id, string Name, string Version, DateTimeOffset ImportedAt, IReadOnlyDictionary<string, int> Counts);

/// <summary>Result of <c>POST /api/v1/admin/content-packs</c>.</summary>
public sealed record ContentPackImportResultDto(string Id, string Name, string Version, IReadOnlyDictionary<string, int> Counts);

/// <summary>A source of catalog definitions: "srd" or an imported content pack (id and display name).</summary>
public sealed record CatalogSourceDto(string Id, string Name, string? Version);

public static class ContentPackLimits
{
    /// <summary>Maximum size of a pack file (20 MB).</summary>
    public const long MaxFileBytes = 20L * 1024 * 1024;
}

/// <summary>
/// The pack is not valid. <see cref="Errors"/> lists every problem as "path: message" in Spanish, e.g.
/// <c>items[3].modifiers[0].kind: Tipo de modificador desconocido.</c>
/// </summary>
public sealed class ContentPackInvalidException(IReadOnlyList<string> errors)
    : Exception(errors.Count == 1 ? errors[0] : $"El paquete de contenido tiene {errors.Count} errores.")
{
    public IReadOnlyList<string> Errors { get; } = errors;
}
