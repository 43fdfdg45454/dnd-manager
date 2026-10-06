namespace Dnd.Domain.Characters;

/// <summary>Why a character must prepare its spells (see <see cref="Character.SpellPreparationPending"/>).</summary>
public enum SpellPreparationReason
{
    /// <summary>First preparation: a new character that prepares spells entered play without one.</summary>
    Creation,

    /// <summary>After a long rest.</summary>
    LongRest,

    /// <summary>After gaining a level in a class that prepares spells.</summary>
    LevelUp,
}
