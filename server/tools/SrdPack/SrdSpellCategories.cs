using OpenTrpg.Core.Domain.Catalog;
using OpenTrpg.Systems.Dnd5e.Domain.Catalog;

namespace OpenTrpg.Tools.SrdPack;

/// <summary>
/// Category of the SRD spells: derived from the dataset (<see cref="SpellCategories.Derive"/>: healing at slot level,
/// damage, saving throw) and corrected by a curated table for what the data cannot tell (buffs, defenses, summons)
/// and for the obvious misses of the derivation (e.g. <c>light</c> has a saving throw but is utility).
/// </summary>
internal static class SrdSpellCategories
{
    /// <summary>Curated categories by SRD spell index; they win over the derived one.</summary>
    public static readonly IReadOnlyDictionary<string, SpellCategory> Overrides = Build(
        (SpellCategory.Healing,
        [
            "goodberry", "greater-restoration", "lesser-restoration", "raise-dead", "reincarnate", "resurrection",
            "revivify", "spare-the-dying", "true-resurrection",
        ]),
        (SpellCategory.Buff,
        [
            "aid", "beacon-of-hope", "bless", "darkvision", "divine-favor", "enhance-ability", "enlarge-reduce",
            "expeditious-retreat", "fly", "foresight", "freedom-of-movement", "greater-invisibility", "guidance", "haste",
            "heroes-feast", "heroism", "hunters-mark", "invisibility", "jump", "longstrider", "magic-weapon", "resistance",
            "see-invisibility", "shillelagh", "spider-climb", "true-seeing", "true-strike", "water-breathing", "water-walk",
        ]),
        (SpellCategory.Defense,
        [
            "antilife-shell", "barkskin", "blink", "blur", "death-ward", "dispel-evil-and-good", "false-life", "fire-shield",
            "globe-of-invulnerability", "holy-aura", "mage-armor", "magic-circle", "mind-blank", "mirror-image",
            "prismatic-wall", "protection-from-energy", "protection-from-evil-and-good", "protection-from-poison",
            "sanctuary", "shield", "shield-of-faith", "stoneskin", "tiny-hut", "wall-of-force", "warding-bond", "wind-wall",
        ]),
        (SpellCategory.Summoning,
        [
            "animate-dead", "animate-objects", "conjure-animals", "conjure-celestial", "conjure-elemental", "conjure-fey",
            "conjure-minor-elementals", "conjure-woodland-beings", "create-undead", "faithful-hound", "find-familiar",
            "find-steed", "gate", "giant-insect", "guardian-of-faith", "phantom-steed", "planar-ally", "simulacrum",
            "unseen-servant",
        ]),
        (SpellCategory.Control,
        [
            "black-tentacles", "color-spray", "earthquake", "feeblemind", "forcecage", "irresistible-dance", "maze",
            "power-word-stun", "silence", "sleep", "sleet-storm", "spike-growth", "symbol", "web",
        ]),
        (SpellCategory.Damage,
        [
            "arcane-hand", "power-word-kill", "spirit-guardians",
        ]),
        (SpellCategory.Utility,
        [
            "contact-other-plane", "control-water", "dimension-door", "dream", "hallow", "light", "magic-jar",
            "plane-shift", "scrying",
        ]));

    /// <summary>The curated category of the spell, or the one derived from its data.</summary>
    public static SpellCategory For(string index, bool heals, bool dealsDamage, bool hasSavingThrow) =>
        Overrides.TryGetValue(index, out var category) ? category : SpellCategories.Derive(heals, dealsDamage, hasSavingThrow);

    private static Dictionary<string, SpellCategory> Build(params (SpellCategory Category, string[] Indexes)[] groups)
    {
        var result = new Dictionary<string, SpellCategory>(StringComparer.Ordinal);
        foreach (var (category, indexes) in groups)
        {
            foreach (var index in indexes)
            {
                result.Add(index, category);
            }
        }

        return result;
    }
}
