using OpenTrpg.Core.Domain.Common;

namespace OpenTrpg.Core.Domain.Characters;

/// <summary>
/// A rest the owner of a character asks the DM for (phase 16b). The kind is one of the game system's rest kinds and
/// the payload is the system's data of the rest (in D&amp;D 5e, the hit dice to spend: <c>{"hitDice":{"fighter":2}}</c>);
/// the DM's approval applies the rest with the system's rules. Only <see cref="RestRequestStatus.Pending"/> requests
/// transition; any other transition throws a <see cref="DomainErrorKind.Conflict"/>. At most one pending request per
/// character (unique filtered index).
/// </summary>
public sealed class RestRequest : EntityBase
{
    public const int CommentMaxLength = 1000;

    public const int KindMaxLength = 16;

    private RestRequest()
    {
    }

    public Guid CampaignId { get; private set; }

    public Guid CharacterId { get; private set; }

    public Guid RequestedByUserId { get; private set; }

    /// <summary>One of the rest kinds of the game system (for example <c>Short</c> or <c>Long</c>).</summary>
    public string Kind { get; private set; } = string.Empty;

    /// <summary>The game system's data of the rest, as a JSON object (<c>{}</c> when there is none).</summary>
    public string PayloadJson { get; private set; } = "{}";

    public RestRequestStatus Status { get; private set; }

    public DateTimeOffset RequestedAt { get; private set; }

    public Guid? ResolvedByUserId { get; private set; }

    public DateTimeOffset? ResolvedAt { get; private set; }

    public string? Comment { get; private set; }

    public bool IsPending => Status == RestRequestStatus.Pending;

    /// <summary>Creates a pending request; the game system has already checked the kind and the payload.</summary>
    public static RestRequest Create(
        Guid campaignId,
        Guid characterId,
        Guid requestedByUserId,
        string kind,
        string payloadJson,
        DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(kind) || kind.Length > KindMaxLength)
        {
            throw DomainException.RuleViolation("El tipo de descanso no es válido.");
        }

        return new RestRequest
        {
            CampaignId = campaignId,
            CharacterId = characterId,
            RequestedByUserId = requestedByUserId,
            Kind = kind,
            PayloadJson = string.IsNullOrWhiteSpace(payloadJson) ? "{}" : payloadJson,
            Status = RestRequestStatus.Pending,
            RequestedAt = now,
            CreatedAt = now,
        };
    }

    /// <summary>Marks the request approved. The caller applies the rest in the same transaction.</summary>
    public void Approve(Guid resolvedByUserId, string? comment, DateTimeOffset now) =>
        Resolve(RestRequestStatus.Approved, resolvedByUserId, comment, now);

    /// <summary>The DM rejects the request; the comment is optional.</summary>
    public void Reject(Guid resolvedByUserId, string? comment, DateTimeOffset now) =>
        Resolve(RestRequestStatus.Rejected, resolvedByUserId, comment, now);

    /// <summary>
    /// Withdraws the request: the owner cancels it, or a DM's direct rest makes it moot. Who may cancel
    /// is decided by the caller.
    /// </summary>
    public void Cancel(Guid actorUserId, DateTimeOffset now) =>
        Resolve(RestRequestStatus.Cancelled, actorUserId, null, now);

    private static string? NormalizeComment(string? comment)
    {
        var trimmed = comment?.Trim() ?? string.Empty;
        if (trimmed.Length > CommentMaxLength)
        {
            throw DomainException.RuleViolation($"El comentario no puede superar los {CommentMaxLength} caracteres.");
        }

        return trimmed.Length == 0 ? null : trimmed;
    }

    private void Resolve(RestRequestStatus status, Guid resolvedByUserId, string? comment, DateTimeOffset now)
    {
        if (Status != RestRequestStatus.Pending)
        {
            throw DomainException.Conflict("La petición de descanso ya no está pendiente.");
        }

        var normalized = NormalizeComment(comment);
        Status = status;
        ResolvedByUserId = resolvedByUserId;
        ResolvedAt = now;
        Comment = normalized;
    }
}
