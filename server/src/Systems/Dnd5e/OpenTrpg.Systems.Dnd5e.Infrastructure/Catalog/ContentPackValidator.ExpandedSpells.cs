using OpenTrpg.Core.Domain.Catalog;
using OpenTrpg.Core.Infrastructure;
using OpenTrpg.Core.Infrastructure.Catalog;
using OpenTrpg.Systems.Dnd5e.Domain.Catalog;
using OpenTrpg.Systems.Dnd5e.Infrastructure.Catalog;

namespace OpenTrpg.Systems.Dnd5e.Infrastructure.Catalog;

// Expanded spell lists of subclasses (phase 25, block 4): spells a subclass adds to its class's spell list. Each one
// must exist in the catalog (another source) or in the pack, with the declared level; they are checked once every
// spell of the pack is known.
internal sealed partial class ContentPackValidator
{
    private readonly List<(string Path, string Index, int Level)> _expandedSpellReferences = [];

    /// <summary>The validated list of <c>subclasses[].expandedSpellList</c>, or null when it is empty.</summary>
    private string? ExpandedSpellList(string path, List<PackExpandedSpellJson?> list)
    {
        var spells = new List<ExpandedSpell>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        ForEach(path, list, (spellPath, spell) =>
        {
            var index = Reference($"{spellPath}.index", spell.Index);
            var level = RequiredInt($"{spellPath}.level", spell.Level, 0, 9);
            if (index is null)
            {
                if (string.IsNullOrWhiteSpace(spell.Index))
                {
                    AddError($"{spellPath}.index", "Campo obligatorio.");
                }

                return;
            }

            if (!seen.Add(index))
            {
                AddError($"{spellPath}.index", $"El conjuro '{index}' está repetido en la lista ampliada.");
                return;
            }

            if (level is { } l)
            {
                _expandedSpellReferences.Add((spellPath, index, l));
                spells.Add(new ExpandedSpell(index, l));
            }
        });

        return ExpandedSpell.ToJson(spells);
    }

    /// <summary>Checks that the spells of the expanded lists exist (catalog of other sources or the pack) with that level.</summary>
    private void CheckExpandedSpellReferences(ContentPackRows rows)
    {
        if (_expandedSpellReferences.Count == 0)
        {
            return;
        }

        var levels = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var (index, source) in _context.Spells ?? new Dictionary<string, string>())
        {
            if (IsOtherSource(source) && _context.SpellLevels?.TryGetValue(index, out var level) == true)
            {
                levels[index] = level;
            }
        }

        foreach (var spell in rows.Spells)
        {
            levels[spell.Index] = spell.Level;
        }

        foreach (var (path, index, level) in _expandedSpellReferences)
        {
            if (!levels.TryGetValue(index, out var actual))
            {
                AddError($"{path}.index", $"El conjuro '{index}' no existe en el catálogo ni en el paquete.");
            }
            else if (actual != level)
            {
                AddError($"{path}.level", $"El conjuro '{index}' es de nivel {actual}, no {level}.");
            }
        }
    }
}
