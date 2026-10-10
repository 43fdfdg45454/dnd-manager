using System.Text.Json;
using OpenTrpg.Core.Domain.Common;

namespace OpenTrpg.Core.Domain.Characters;

/// <summary>
/// A short or long rest the owner of a character asks the DM for (phase 16b). A short rest carries the
/// hit dice the player wants to spend (<c>{"fighter":2}</c>); the DM's approval applies the rest with
/// the PHB rules. Only <see cref="RestRequestStatus.Pending"/> requests transition; any other transition
/// throws a <see cref="DomainErrorKind.Conflict"/>. At most one pending request per character (unique
/// filtered index).
/// </summary>
public sealed class RestRequest : EntityBase
{
    public const int CommentMaxLength = 1000;

    /// <summary>Upper bound of dice per class in a request (a character never has more than 20 levels).</summary>
    public const int MaxHitDicePerClass = 20;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private RestRequest()
    {
    }

    public Guid CampaignId { get; private set; }

    public Guid CharacterId { get; private set; }

    public Guid RequestedByUserId { get; private set; }

    public RestKind Kind { get; private set; }

    /// <summary>Persisted form of <see cref="HitDice"/>: <c>{"fighter":2}</c> (always <c>{}</c> for a long rest).</summary>
    public string HitDiceJson { get; private set; } = "{}";

    public RestRequestStatus Status { get; private set; }

    public DateTimeOffset RequestedAt { get; private set; }

    public Guid? ResolvedByUserId { get; private set; }

    public DateTimeOffset? ResolvedAt { get; private set; }

    public string? Comment { get; private set; }

    public bool IsPending => Status == RestRequestStatus.Pending;

    /// <summary>Hit dice to spend per class index (short rest only).</summary>
    public IReadOnlyDictionary<string, int> HitDice =>
        JsonSerializer.Deserialize<Dictionary<string, int>>(HitDiceJson, JsonOptions) ?? [];

    /// <summary>
    /// Creates a pending request. Hit dice only make sense for a short rest; entries with 0 dice are
    /// dropped and class indexes are trimmed.
    /// </summary>
    public static RestRequest Create(
        Guid campaignId,
        Guid characterId,
        Guid requestedByUserId,
        RestKind kind,
        IReadOnlyDictionary<string, int>? hitDice,
        DateTimeOffset now)
    {
        if (!Enum.IsDefined(kind))
        {
            throw DomainException.RuleViolation("El tipo de descanso no es válido.");
        }

        var dice = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var (classIndex, count) in hitDice ?? new Dictionary<string, int>())
        {
            if (string.IsNullOrWhiteSpace(classIndex))
            {
                throw DomainException.RuleViolation("Indica la clase de cada dado de golpe.");
            }

            if (count is < 0 or > MaxHitDicePerClass)
            {
                throw DomainException.RuleViolation($"Cada clase debe indicar entre 0 y {MaxHitDicePerClass} dados de golpe.");
            }

            if (count > 0)
            {
                var key = classIndex.Trim();
                dice[key] = dice.GetValueOrDefault(key) + count;
            }
        }

        if (kind == RestKind.Long && dice.Count > 0)
        {
            throw DomainException.RuleViolation("El descanso largo no gasta dados de golpe.");
        }

        return new RestRequest
        {
            CampaignId = campaignId,
            CharacterId = characterId,
            RequestedByUserId = requestedByUserId,
            Kind = kind,
            HitDiceJson = JsonSerializer.Serialize(dice, JsonOptions),
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
