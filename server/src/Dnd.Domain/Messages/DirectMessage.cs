using Dnd.Domain.Common;

namespace Dnd.Domain.Messages;

/// <summary>
/// Secret message from a DM to the player of a character (markdown). Only the sender's side (the
/// DMs) and its recipient (the owner of the character when it was sent) can see it.
/// </summary>
public sealed class DirectMessage : EntityBase
{
    public const int BodyMaxLength = 2000;

    private DirectMessage()
    {
    }

    public Guid CampaignId { get; private set; }

    public Guid SenderUserId { get; private set; }

    public Guid RecipientUserId { get; private set; }

    /// <summary>Character the message is addressed to.</summary>
    public Guid CharacterId { get; private set; }

    /// <summary>Markdown text, trimmed, 1 to <see cref="BodyMaxLength"/> characters.</summary>
    public string Body { get; private set; } = string.Empty;

    public DateTimeOffset SentAt { get; private set; }

    /// <summary>When the recipient first read it; null while unread.</summary>
    public DateTimeOffset? ReadAt { get; private set; }

    public bool IsRead => ReadAt is not null;

    public static DirectMessage Create(Guid campaignId, Guid senderUserId, Guid recipientUserId, Guid characterId, string body, DateTimeOffset now)
    {
        var trimmed = (body ?? string.Empty).Trim();
        if (trimmed.Length is 0 or > BodyMaxLength)
        {
            throw DomainException.RuleViolation($"El mensaje debe tener entre 1 y {BodyMaxLength} caracteres.");
        }

        return new DirectMessage
        {
            CampaignId = campaignId,
            SenderUserId = senderUserId,
            RecipientUserId = recipientUserId,
            CharacterId = characterId,
            Body = trimmed,
            SentAt = now,
            CreatedAt = now,
        };
    }

    /// <summary>Marks the message as read; reading it again keeps the first time.</summary>
    public void MarkRead(DateTimeOffset now)
    {
        ReadAt ??= now;
    }
}
