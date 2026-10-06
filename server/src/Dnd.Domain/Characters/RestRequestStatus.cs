namespace Dnd.Domain.Characters;

/// <summary>State of a <see cref="RestRequest"/>. Only <see cref="Pending"/> can transition; the others are final.</summary>
public enum RestRequestStatus
{
    Pending,
    Approved,
    Rejected,
    Cancelled,
}
