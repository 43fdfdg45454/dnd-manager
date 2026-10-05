using Dnd.Domain.Common;

namespace Dnd.Domain.Characters;

/// <summary>
/// A change to a character asked by its owner that a DM must approve. The payload is opaque JSON for
/// the domain (a serialized sheet edit, item operation...); the application applies it on approval with
/// the same logic as a direct edit. Only <see cref="ChangeRequestStatus.Pending"/> requests transition;
/// any other transition throws a <see cref="DomainErrorKind.Conflict"/>.
/// </summary>
public sealed class ChangeRequest : EntityBase
{
    public const int CommentMaxLength = 1000;

    private ChangeRequest()
    {
    }

    public Guid CampaignId { get; private set; }

    public Guid CharacterId { get; private set; }

    public Guid RequestedByUserId { get; private set; }

    public ChangeRequestType Type { get; private set; }

    public string PayloadJson { get; private set; } = "{}";

    public ChangeRequestStatus Status { get; private set; }

    public Guid? ResolvedByUserId { get; private set; }

    public DateTimeOffset? ResolvedAt { get; private set; }

    public string? Comment { get; private set; }

    public bool IsPending => Status == ChangeRequestStatus.Pending;

    /// <summary>Creates a pending request. A null or blank payload is stored as <c>{}</c>.</summary>
    public static ChangeRequest Create(
        Guid campaignId,
        Guid characterId,
        Guid requestedByUserId,
        ChangeRequestType type,
        string? payloadJson,
        DateTimeOffset now)
    {
        if (!Enum.IsDefined(type))
        {
            throw DomainException.RuleViolation("El tipo de solicitud no es válido.");
        }

        return new ChangeRequest
        {
            CampaignId = campaignId,
            CharacterId = characterId,
            RequestedByUserId = requestedByUserId,
            Type = type,
            PayloadJson = string.IsNullOrWhiteSpace(payloadJson) ? "{}" : payloadJson,
            Status = ChangeRequestStatus.Pending,
            CreatedAt = now,
        };
    }

    /// <summary>Marks the request approved. The caller applies the payload in the same transaction.</summary>
    public void Approve(Guid resolvedByUserId, string? comment, DateTimeOffset now)
    {
        EnsurePending();
        Resolve(ChangeRequestStatus.Approved, resolvedByUserId, NormalizeComment(comment), now);
    }

    /// <summary>Rejects the request; a comment explaining why is required.</summary>
    public void Reject(Guid resolvedByUserId, string comment, DateTimeOffset now)
    {
        EnsurePending();
        var normalized = NormalizeComment(comment)
            ?? throw DomainException.RuleViolation("Hay que indicar el motivo del rechazo.");
        Resolve(ChangeRequestStatus.Rejected, resolvedByUserId, normalized, now);
    }

    /// <summary>The requester withdraws a pending request.</summary>
    public void Cancel(Guid actorUserId, DateTimeOffset now)
    {
        if (actorUserId != RequestedByUserId)
        {
            throw DomainException.Forbidden("Solo quien hizo la solicitud puede cancelarla.");
        }

        EnsurePending();
        Resolve(ChangeRequestStatus.Cancelled, actorUserId, null, now);
    }

    private static string? NormalizeComment(string? comment)
    {
        var trimmed = comment?.Trim() ?? string.Empty;
        if (trimmed.Length > CommentMaxLength)
        {
            throw DomainException.RuleViolation($"El comentario no puede superar los {CommentMaxLength} caracteres.");
        }

        return trimmed.Length == 0 ? null : trimmed;
    }

    private void EnsurePending()
    {
        if (Status != ChangeRequestStatus.Pending)
        {
            throw DomainException.Conflict("La solicitud ya no está pendiente.");
        }
    }

    private void Resolve(ChangeRequestStatus status, Guid resolvedByUserId, string? comment, DateTimeOffset now)
    {
        Status = status;
        ResolvedByUserId = resolvedByUserId;
        ResolvedAt = now;
        Comment = comment;
    }
}
