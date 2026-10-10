namespace OpenTrpg.Core.Domain.Characters;

/// <summary>Outcome of <see cref="Character.ResolveSheetEdit"/>: whether a sheet edit applies now or needs DM approval.</summary>
public enum SheetEditMode
{
    /// <summary>The edit is applied immediately (DM, or owner while the character is a draft).</summary>
    Direct,

    /// <summary>The owner of an active character: the edit becomes a pending <see cref="ChangeRequest"/>.</summary>
    RequiresApproval,
}
