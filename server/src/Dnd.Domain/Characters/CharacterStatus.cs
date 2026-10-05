namespace Dnd.Domain.Characters;

/// <summary>Lifecycle of a character. In <see cref="Draft"/> the owner edits freely; once <see cref="Active"/>, owner edits need DM approval.</summary>
public enum CharacterStatus
{
    Draft,
    Active,
}
