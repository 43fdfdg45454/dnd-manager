using Dnd.Domain.Common;
using Dnd.Domain.Sessions;

namespace Dnd.Domain.Campaigns;

/// <summary>
/// Campaign aggregate: owns its members and enforces the membership rules. Every operation takes
/// the id of the acting user and checks the actor's role against the members of the campaign.
/// The owner is always a member with role <see cref="CampaignRole.Owner"/>, and is the only one.
/// </summary>
public sealed class Campaign : EntityBase
{
    public const int NameMaxLength = 100;
    public const int DescriptionMaxLength = 2000;

    /// <summary>Upper bound of the shared gold of the party stash, in copper pieces.</summary>
    public const long MaxStashCopperPieces = 1_000_000_000_000;

    private readonly List<CampaignMember> _members = [];

    private Campaign()
    {
    }

    public string Name { get; private set; } = string.Empty;

    /// <summary>Markdown text, possibly empty.</summary>
    public string Description { get; private set; } = string.Empty;

    public Guid OwnerId { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>IANA time zone (for example <c>Europe/Madrid</c>) in which the campaign's sessions are shown and announced.</summary>
    public string TimeZoneId { get; private set; } = CampaignSchedule.FallbackTimeZoneId;

    /// <summary>JSON array of minutes before a session at which email reminders are sent.</summary>
    public string ReminderOffsetsMinutesJson { get; private set; } = CampaignSchedule.SerializeOffsets(CampaignSchedule.DefaultOffsetsMinutes);

    public IReadOnlyList<int> ReminderOffsetsMinutes => CampaignSchedule.ParseOffsets(ReminderOffsetsMinutesJson);

    /// <summary>Whether players can take items from the party stash (and give them back) by themselves.</summary>
    public bool PlayersCanTakeFromStash { get; private set; } = true;

    /// <summary>
    /// Shared gold of the party stash, in copper pieces (≥ 0). Also an optimistic concurrency token:
    /// two concurrent changes computed from the same amount cannot both be saved.
    /// </summary>
    public long StashCopperPieces { get; private set; }

    public IReadOnlyCollection<CampaignMember> Members => _members;

    /// <summary>
    /// Creates a campaign whose creator becomes its owner (and only member). A null time zone uses
    /// <see cref="CampaignSchedule.FallbackTimeZoneId"/>.
    /// </summary>
    public static Campaign Create(string name, string? description, Guid ownerId, DateTimeOffset now, string? timeZoneId = null)
    {
        var campaign = new Campaign
        {
            OwnerId = ownerId,
            CreatedAt = now,
            UpdatedAt = now,
            TimeZoneId = CampaignSchedule.RequireTimeZone(timeZoneId ?? CampaignSchedule.FallbackTimeZoneId),
        };
        campaign.SetName(name);
        campaign.SetDescription(description);
        campaign._members.Add(CampaignMember.Create(campaign.Id, ownerId, CampaignRole.Owner, now));
        return campaign;
    }

    public CampaignMember? FindMember(Guid userId) => _members.FirstOrDefault(m => m.UserId == userId);

    public CampaignRole? RoleOf(Guid userId) => FindMember(userId)?.Role;

    /// <summary>Changes name and/or description (null keeps the current value). Requires at least DM.</summary>
    public void UpdateDetails(Guid actorUserId, string? name, string? description, DateTimeOffset now)
    {
        RequireActor(actorUserId, CampaignRole.DM, "Solo el propietario o un DM pueden editar la campaña.");

        if (name is not null)
        {
            SetName(name);
        }

        if (description is not null)
        {
            SetDescription(description);
        }

        UpdatedAt = now;
    }

    /// <summary>
    /// Changes the time zone, the reminder offsets and/or whether players take from the party stash
    /// (null keeps the current value). Requires at least DM. Returns true when the offsets changed, so
    /// pending reminders must be regenerated.
    /// </summary>
    public bool UpdateSettings(
        Guid actorUserId,
        string? timeZoneId,
        IReadOnlyCollection<int>? reminderOffsetsMinutes,
        DateTimeOffset now,
        bool? playersCanTakeFromStash = null)
    {
        RequireActor(actorUserId, CampaignRole.DM, "Solo el propietario o un DM pueden editar los ajustes de la campaña.");

        var newZone = timeZoneId is null ? TimeZoneId : CampaignSchedule.RequireTimeZone(timeZoneId);
        var newOffsetsJson = reminderOffsetsMinutes is null
            ? ReminderOffsetsMinutesJson
            : CampaignSchedule.SerializeOffsets(CampaignSchedule.RequireOffsets(reminderOffsetsMinutes));

        var offsetsChanged = newOffsetsJson != ReminderOffsetsMinutesJson;
        TimeZoneId = newZone;
        ReminderOffsetsMinutesJson = newOffsetsJson;
        PlayersCanTakeFromStash = playersCanTakeFromStash ?? PlayersCanTakeFromStash;
        UpdatedAt = now;
        return offsetsChanged;
    }

    /// <summary>
    /// Adds (or, when negative, withdraws) shared gold of the party stash. The result must stay between
    /// 0 and <see cref="MaxStashCopperPieces"/>. Permissions are checked by the caller.
    /// </summary>
    public void AdjustStashGold(long deltaCp, DateTimeOffset now)
    {
        var result = StashCopperPieces + deltaCp;
        if (result < 0)
        {
            throw DomainException.RuleViolation("No hay tanto oro en el alijo del grupo.");
        }

        if (result > MaxStashCopperPieces)
        {
            throw DomainException.RuleViolation($"El oro del alijo no puede superar {MaxStashCopperPieces} pc.");
        }

        StashCopperPieces = result;
        UpdatedAt = now;
    }

    /// <summary>
    /// Splits the shared gold in equal shares of copper pieces among <paramref name="recipients"/>:
    /// takes <c>share × recipients</c> out of the stash (the remainder stays) and returns the share.
    /// </summary>
    public long SplitStashGold(int recipients, DateTimeOffset now)
    {
        if (recipients < 1)
        {
            throw DomainException.RuleViolation("No hay personajes entre los que repartir el oro.");
        }

        var share = StashCopperPieces / recipients;
        if (share == 0)
        {
            throw DomainException.RuleViolation("No hay oro suficiente en el alijo para repartir.");
        }

        StashCopperPieces -= share * recipients;
        UpdatedAt = now;
        return share;
    }

    /// <summary>
    /// Adds a member. At least DM can add players; only the owner can add DMs. The owner role is
    /// never assigned here.
    /// </summary>
    public CampaignMember AddMember(Guid actorUserId, Guid userId, CampaignRole role, DateTimeOffset now)
    {
        var actor = RequireActor(actorUserId, CampaignRole.DM, "Solo el propietario o un DM pueden añadir miembros.");
        EnsureAssignable(role);

        if (role == CampaignRole.DM && actor.Role != CampaignRole.Owner)
        {
            throw DomainException.Forbidden("Solo el propietario puede añadir DMs.");
        }

        if (FindMember(userId) is not null)
        {
            throw DomainException.Conflict("El usuario ya es miembro de la campaña.");
        }

        var member = CampaignMember.Create(Id, userId, role, now);
        _members.Add(member);
        return member;
    }

    /// <summary>Switches a member between DM and Player. Owner only; the owner's role cannot change here.</summary>
    public CampaignMember ChangeRole(Guid actorUserId, Guid userId, CampaignRole role)
    {
        RequireActor(actorUserId, CampaignRole.Owner, "Solo el propietario puede cambiar roles.");
        EnsureAssignable(role);

        var member = FindMember(userId) ?? throw MemberNotFound();
        if (member.Role == CampaignRole.Owner)
        {
            throw DomainException.RuleViolation("No se puede cambiar el rol del propietario. Transfiere la propiedad en su lugar.");
        }

        member.ChangeRole(role);
        return member;
    }

    /// <summary>
    /// Removes another member. The owner can remove anyone but themselves; a DM can only remove
    /// players. The owner can never be removed.
    /// </summary>
    public void RemoveMember(Guid actorUserId, Guid userId)
    {
        var actor = RequireActor(actorUserId, CampaignRole.DM, "Solo el propietario o un DM pueden quitar miembros.");

        var member = FindMember(userId) ?? throw MemberNotFound();
        if (member.Role == CampaignRole.Owner)
        {
            throw DomainException.RuleViolation("No se puede quitar al propietario de la campaña.");
        }

        if (actor.Role != CampaignRole.Owner && member.Role != CampaignRole.Player)
        {
            throw DomainException.Forbidden("Un DM solo puede quitar a jugadores.");
        }

        _members.Remove(member);
    }

    /// <summary>The member leaves the campaign. The owner must transfer the ownership first.</summary>
    public void Leave(Guid userId)
    {
        var member = FindMember(userId) ?? throw MemberNotFound();
        if (member.Role == CampaignRole.Owner)
        {
            throw DomainException.RuleViolation("El propietario no puede salir de la campaña. Transfiere la propiedad antes.");
        }

        _members.Remove(member);
    }

    /// <summary>
    /// Makes another existing member the owner. The previous owner stays as DM or Player, as given.
    /// Returns the audit record, which the caller must persist.
    /// </summary>
    public OwnershipTransfer TransferOwnership(Guid actorUserId, Guid toUserId, CampaignRole previousOwnerNewRole, DateTimeOffset now)
    {
        var actor = RequireActor(actorUserId, CampaignRole.Owner, "Solo el propietario puede transferir la propiedad.");

        if (previousOwnerNewRole is not (CampaignRole.DM or CampaignRole.Player))
        {
            throw DomainException.RuleViolation("El rol que conservas debe ser DM o Player.");
        }

        if (toUserId == actorUserId)
        {
            throw DomainException.RuleViolation("No puedes transferirte la propiedad a ti mismo.");
        }

        var target = FindMember(toUserId)
            ?? throw DomainException.RuleViolation("El nuevo propietario debe ser miembro de la campaña.");

        target.ChangeRole(CampaignRole.Owner);
        actor.ChangeRole(previousOwnerNewRole);
        OwnerId = toUserId;
        UpdatedAt = now;

        return OwnershipTransfer.Create(Id, actorUserId, toUserId, previousOwnerNewRole, now);
    }

    private CampaignMember RequireActor(Guid actorUserId, CampaignRole minimum, string forbiddenMessage)
    {
        var actor = FindMember(actorUserId) ?? throw DomainException.Forbidden("No eres miembro de la campaña.");
        return actor.Role.IsAtLeast(minimum) ? actor : throw DomainException.Forbidden(forbiddenMessage);
    }

    private static void EnsureAssignable(CampaignRole role)
    {
        if (role is not (CampaignRole.DM or CampaignRole.Player))
        {
            throw DomainException.RuleViolation("El rol debe ser DM o Player. La propiedad solo se cambia mediante una transferencia.");
        }
    }

    private static DomainException MemberNotFound() => DomainException.NotFound("El usuario no es miembro de la campaña.");

    private void SetName(string name)
    {
        var trimmed = name.Trim();
        if (trimmed.Length is 0 or > NameMaxLength)
        {
            throw DomainException.RuleViolation($"El nombre debe tener entre 1 y {NameMaxLength} caracteres.");
        }

        Name = trimmed;
    }

    private void SetDescription(string? description)
    {
        var value = description ?? string.Empty;
        if (value.Length > DescriptionMaxLength)
        {
            throw DomainException.RuleViolation($"La descripción no puede superar los {DescriptionMaxLength} caracteres.");
        }

        Description = value;
    }
}
