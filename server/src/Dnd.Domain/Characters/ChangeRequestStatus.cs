namespace Dnd.Domain.Characters;

/// <summary>State of a <see cref="ChangeRequest"/>. Only <see cref="Pending"/> can transition; the others are final.</summary>
public enum ChangeRequestStatus
{
    Pending,
    Approved,
    Rejected,
    Cancelled,
}
