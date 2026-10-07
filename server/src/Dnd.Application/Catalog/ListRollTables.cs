using Dnd.Application.Abstractions.Persistence;

namespace Dnd.Application.Catalog;

public sealed record RollTableEntryDto(int From, int To, string Text);

/// <param name="Dice">"d100", "d20"...</param>
/// <param name="Source">Id of the content pack that defines the table.</param>
public sealed record RollTableDto(
    string Key,
    string Name,
    string Dice,
    string? ClassIndex,
    string? SubclassIndex,
    string Source,
    IReadOnlyList<RollTableEntryDto> Entries);

/// <summary>Filters of <c>GET /catalog/roll-tables</c>.</summary>
/// <param name="Subclass">Only the tables of this subclass.</param>
/// <param name="Class">Only the tables of this class (including those of its subclasses).</param>
public sealed record ListRollTablesQuery(string? Subclass = null, string? Class = null);

/// <summary>
/// Roll tables of the content packs (phase 22), e.g. the d100 Wild Magic Surge of a sorcerer subclass. They only come
/// from packs, so the list is empty on a pure SRD instance.
/// </summary>
public sealed class ListRollTablesHandler(ICatalogRepository catalog)
{
    public async Task<IReadOnlyList<RollTableDto>> HandleAsync(ListRollTablesQuery query, CancellationToken cancellationToken = default)
    {
        var subclass = query.Subclass?.Trim();
        var classIndex = query.Class?.Trim();
        return (await catalog.ListRollTablesAsync(cancellationToken))
            .Where(t => string.IsNullOrEmpty(subclass) || t.SubclassIndex == subclass)
            .Where(t => string.IsNullOrEmpty(classIndex) || t.ClassIndex == classIndex)
            .Select(t => new RollTableDto(
                t.Key,
                t.Name,
                t.Dice,
                t.ClassIndex,
                t.SubclassIndex,
                t.Source,
                t.Entries.Select(e => new RollTableEntryDto(e.From, e.To, e.Text)).ToList()))
            .ToList();
    }
}
