using System.Text;
using OpenTrpg.Systems.Dnd5e.Application.Abstractions.Persistence;

namespace OpenTrpg.Systems.Dnd5e.Application.Catalog;

/// <summary>
/// Resolves the fixed tool proficiencies of a class to catalog item indexes. A class lists its proficiencies as
/// texts ("Light Armor", "Simple Weapons", "Thieves' Tools"): the entries that are an item index, or whose index form
/// ("Thieves' Tools" → "thieves-tools") is one, are the tools; the rest (armor and weapon groups) are left out.
/// </summary>
public static class ToolProficiencies
{
    public static async Task<IReadOnlyList<string>> ResolveAsync(ICatalogRepository catalog, IReadOnlyList<string> proficiencyNames, CancellationToken cancellationToken)
    {
        var candidates = proficiencyNames
            .Select(name => (Name: name, Indexes: new[] { name.Trim(), IndexOf(name) }.Distinct(StringComparer.Ordinal).ToList()))
            .ToList();
        var known = (await catalog.ListCatalogItemsByIndexAsync(candidates.SelectMany(c => c.Indexes).Distinct(StringComparer.Ordinal).ToList(), cancellationToken))
            .Select(i => i.Index!)
            .ToHashSet(StringComparer.Ordinal);
        return candidates
            .Select(c => c.Indexes.FirstOrDefault(known.Contains))
            .OfType<string>()
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>The index form of a name: lowercase, apostrophes dropped and any other separator turned into a hyphen.</summary>
    public static string IndexOf(string name)
    {
        var result = new StringBuilder(name.Length);
        foreach (var c in name.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(c))
            {
                result.Append(c);
            }
            else if (c is '\'' or '\u2019')
            {
                continue;
            }
            else if (result.Length > 0 && result[^1] != '-')
            {
                result.Append('-');
            }
        }

        return result.ToString().TrimEnd('-');
    }
}
