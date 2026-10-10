namespace OpenTrpg.Core.Application.Systems;

/// <summary>
/// The rules catalog of a game system: its base pack (loaded at startup) and the content packs the administrator
/// imports. The core keeps the registry of packs and the transaction (<c>ContentPackRegistry</c>); the system validates
/// a pack and writes or deletes its definitions.
/// </summary>
public interface ICatalogSystem
{
    /// <summary>Kinds of definition the system's packs carry (classes, races, spells, items…).</summary>
    IReadOnlyList<string> DefinitionTypes { get; }

    /// <summary>True for source ids a content pack cannot take (the base pack of the system).</summary>
    bool IsReservedSource(string id);

    /// <summary>Loads the base pack (the SRD in D&amp;D 5e) when it is not loaded yet. Idempotent.</summary>
    Task LoadBasePackAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates a pack (throws <c>ContentPackInvalidException</c> with every problem) and replaces its definitions,
    /// inside the transaction the core opened. Returns the header of the pack and the definitions written per type.
    /// </summary>
    Task<PackImportResult> ImportPackAsync(Stream json, CancellationToken cancellationToken = default);

    /// <summary>Deletes the definitions of a pack (inside the transaction the core opened).</summary>
    Task DeletePackAsync(string packId, CancellationToken cancellationToken = default);
}

/// <summary>A pack the system imported: its header and the definitions written per type.</summary>
public sealed record PackImportResult(string Id, string Name, string Version, IReadOnlyDictionary<string, int> Counts);
