using Dnd.Application.ContentPacks;

namespace Dnd.Application.Abstractions;

/// <summary>Imports, lists and deletes the private content packs of the instance (see <c>docs/content-packs.md</c>).</summary>
public interface IContentPackImporter
{
    /// <summary>
    /// Validates the pack JSON and replaces every definition of the pack in one transaction (item ids are kept).
    /// Throws <see cref="ContentPackInvalidException"/> with every problem found when the pack is not valid.
    /// </summary>
    Task<ContentPackImportResultDto> ImportAsync(Stream json, CancellationToken cancellationToken = default);

    /// <summary>Imported packs ordered by name.</summary>
    Task<IReadOnlyList<ContentPackDto>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes the definitions of the pack. Items still used by inventories, shops or party stashes are kept so
    /// those entries keep working; the catalog no longer lists them. Returns false when the pack does not exist.
    /// </summary>
    Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default);
}
