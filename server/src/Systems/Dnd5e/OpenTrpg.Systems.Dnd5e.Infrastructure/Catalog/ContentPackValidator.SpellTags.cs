using OpenTrpg.Systems.Dnd5e.Domain.Catalog;

namespace OpenTrpg.Systems.Dnd5e.Infrastructure.Catalog;

// Spell tags (phase 35B3): a vocabulary (reference.spellTags) that tags spells of the pack (spells[].tags) and of the SRD
// or the required packs (reference.spellTags[].spells). The tags filter the compendium and the spell choices
// (filter.tags), and expand subclass spell lists ({ "tags": [...] }).
internal sealed partial class ContentPackValidator
{
    /// <summary>Tags of the pack's own vocabulary (<c>reference.spellTags</c>).</summary>
    private readonly HashSet<string> _packSpellTags = new(StringComparer.Ordinal);

    /// <summary>Spells tagged by <c>reference.spellTags[].spells</c>, checked once every spell of the pack is known.</summary>
    private readonly List<(string Path, string Index)> _taggedSpellReferences = [];

    /// <summary>A tag of the pack's vocabulary or of a required pack's.</summary>
    private bool KnownSpellTag(string tag) =>
        _packSpellTags.Contains(tag)
        || (_context.Vocabularies?.TryGetValue(ReferenceEntry.SpellTags, out var known) == true && known.Contains(tag));

    /// <summary>A validated list of spell tags (each one known, without repeats).</summary>
    private List<string> SpellTagList(string path, List<string?>? tags, int max = SpellTag.MaxTagsPerSpell)
    {
        var result = new List<string>();
        if (tags is { Count: > 0 } && tags.Count > max)
        {
            AddError(path, $"Como máximo {max} etiquetas.");
            return result;
        }

        ForEachText(path, tags, (tagPath, value) =>
        {
            var tag = value.ToLowerInvariant();
            if (!KnownSpellTag(tag))
            {
                AddError(tagPath, $"La etiqueta '{value}' no existe: declárala en reference.spellTags (del paquete o de un paquete requerido).");
            }
            else if (!result.Contains(tag))
            {
                result.Add(tag);
            }
        });
        return result;
    }

    /// <summary>The spells of a <c>reference.spellTags</c> entry: rows of <see cref="SpellTag"/> (the spells are checked later).</summary>
    private void TaggedSpells(string path, string tag, List<string?>? spells, ContentPackRows rows)
    {
        ForEachText(path, spells, (spellPath, value) =>
        {
            if (Reference(spellPath, value) is { } spellIndex && !rows.SpellTags.Any(t => t.Tag == tag && t.SpellIndex == spellIndex))
            {
                _taggedSpellReferences.Add((spellPath, spellIndex));
                rows.SpellTags.Add(new SpellTag { Tag = tag, SpellIndex = spellIndex, Source = _id });
            }
        });
    }

    /// <summary>Checks that the spells tagged by <c>reference.spellTags[].spells</c> exist (catalog of other sources or the pack).</summary>
    private void CheckTaggedSpellReferences(ContentPackRows rows)
    {
        foreach (var (path, index) in _taggedSpellReferences)
        {
            var exists = rows.Spells.Any(s => s.Index == index)
                || (_context.Spells?.TryGetValue(index, out var source) == true && IsOtherSource(source));
            if (!exists)
            {
                AddError(path, $"El conjuro '{index}' no existe en el SRD, en los paquetes requeridos ni en el paquete.");
            }
        }
    }
}
