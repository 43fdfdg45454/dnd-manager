using OpenTrpg.Core.Domain.Common;

namespace OpenTrpg.Core.Domain.Characters;

// Spell preparation (phase 18): clerics, druids, paladins and wizards choose their prepared spells every time
// the rules let them change them (after each long rest, on gaining a level in the class and the first time).
// The app forces the preparation screen while it is pending; the player may keep the previous preparation.
public sealed partial class Dnd5eCharacter
{
    /// <summary>The player must (re)prepare spells before playing on (see <see cref="SpellPreparationReason"/>).</summary>
    public bool SpellPreparationPending { get; private set; }

    /// <summary>Why the preparation is pending; null when nothing is pending.</summary>
    public SpellPreparationReason? SpellPreparationReason { get; private set; }

    /// <summary>Marks the spell preparation as pending (the latest reason wins).</summary>
    public void RequireSpellPreparation(SpellPreparationReason reason, DateTimeOffset now)
    {
        if (!Enum.IsDefined(reason))
        {
            throw new ArgumentOutOfRangeException(nameof(reason), reason, "Unknown spell preparation reason.");
        }

        SpellPreparationPending = true;
        SpellPreparationReason = reason;
        Touch(now);
    }

    /// <summary>The preparation was done (or kept); false when nothing was pending.</summary>
    public bool CompleteSpellPreparation(DateTimeOffset now)
    {
        if (!SpellPreparationPending)
        {
            return false;
        }

        SpellPreparationPending = false;
        SpellPreparationReason = null;
        Touch(now);
        return true;
    }

    /// <summary>
    /// Long rest (see <see cref="LongRest(int, DateTimeOffset)"/>) with the maximum hit points of the sheet; a
    /// character with a class that prepares spells must then prepare them again.
    /// </summary>
    public void LongRest(CharacterSheet sheet, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        LongRest(sheet.HitPointsMax, now);
        if (sheet.PreparingClasses.Count > 0)
        {
            RequireSpellPreparation(Characters.SpellPreparationReason.LongRest, now);
        }
    }

    /// <summary>
    /// Replaces the prepared spells of a class. Always-prepared spells and the spells for which
    /// <paramref name="isPreparable"/> is false (cantrips, spells missing from the catalog) are left untouched.
    /// Every other spell of the class is prepared when it is in <paramref name="spellIndexes"/>; otherwise it is
    /// kept unprepared when <paramref name="keepUnprepared"/> (a wizard's spellbook) or removed. Spells of the list
    /// the character does not have yet are added, prepared. The caller validates the list (candidates, maximum).
    /// </summary>
    public void SetPreparedSpells(
        string classIndex,
        IReadOnlyCollection<string> spellIndexes,
        Func<string, bool> isPreparable,
        bool keepUnprepared,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(spellIndexes);
        ArgumentNullException.ThrowIfNull(isPreparable);
        var characterClass = RequireIndex(classIndex, "clase");
        if (!_classes.Any(c => c.ClassIndex == characterClass))
        {
            throw DomainException.RuleViolation($"El personaje no tiene la clase '{characterClass}'.");
        }

        var wanted = spellIndexes.Select(s => RequireIndex(s, "conjuro")).ToHashSet(StringComparer.Ordinal);
        foreach (var spell in _spells.Where(s => s.ClassIndex == characterClass && !s.AlwaysPrepared && isPreparable(s.SpellIndex)).ToList())
        {
            if (wanted.Contains(spell.SpellIndex))
            {
                spell.Update(new SpellEntry(spell.SpellIndex, characterClass, true));
            }
            else if (keepUnprepared)
            {
                spell.Update(new SpellEntry(spell.SpellIndex, characterClass, false));
            }
            else
            {
                _spells.Remove(spell);
            }
        }

        foreach (var spellIndex in wanted.Where(w => !_spells.Any(s => s.ClassIndex == characterClass && s.SpellIndex == w)))
        {
            _spells.Add(CharacterSpell.Create(Id, new SpellEntry(spellIndex, characterClass, true)));
        }

        Touch(now);
    }
}
