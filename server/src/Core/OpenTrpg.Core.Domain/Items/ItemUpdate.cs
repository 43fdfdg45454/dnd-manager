namespace OpenTrpg.Core.Domain.Items;

/// <summary>
/// Play-state changes of an inventory entry (no approval needed). Null fields are left unchanged;
/// <see cref="Notes"/> and <see cref="Charges"/> are only applied when their <c>Set…</c> flag is true,
/// so that an explicit null clears them.
/// </summary>
public sealed record ItemUpdate
{
    public bool? Equipped { get; init; }

    public bool? Attuned { get; init; }

    /// <summary>
    /// With <see cref="Attuned"/> true: another attuned entry that ends its attunement in the same operation
    /// (the player chose which one to drop when the limit was reached).
    /// </summary>
    public Guid? ReplaceAttunedItemId { get; init; }

    public bool SetNotes { get; init; }

    public string? Notes { get; init; }

    public int? SortOrder { get; init; }

    public bool SetCharges { get; init; }

    /// <summary>Remaining charges (the first value also sets the maximum); null removes the charges.</summary>
    public int? Charges { get; init; }
}
