namespace OpenTrpg.Core.Domain.Characters;

/// <summary>
/// Replaces a calculated value of the sheet. <see cref="Field"/> is one of <see cref="OverrideFields"/>;
/// unique per (CharacterId, Field).
/// </summary>
public sealed class CharacterOverride
{
    public const int NoteMaxLength = 500;

    private CharacterOverride()
    {
    }

    public Guid Id { get; init; } = Guid.NewGuid();

    public Guid CharacterId { get; private set; }

    public string Field { get; private set; } = string.Empty;

    public int Value { get; private set; }

    public string? Note { get; private set; }

    internal static CharacterOverride Create(Guid characterId, OverrideEntry entry) => new()
    {
        CharacterId = characterId,
        Field = entry.Field,
        Value = entry.Value,
        Note = entry.Note,
    };

    internal void Update(OverrideEntry entry)
    {
        Value = entry.Value;
        Note = entry.Note;
    }
}
