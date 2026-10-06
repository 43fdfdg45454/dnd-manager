using Dnd.Application.Abstractions;
using Dnd.Application.Abstractions.Persistence;
using Dnd.Application.Characters;
using Dnd.Application.Common;
using Dnd.Domain.Campaigns;
using Dnd.Domain.Messages;
using FluentValidation;

namespace Dnd.Application.Messages;

// Secret messages from a DM to the players of some characters.

public sealed record MessageDto(
    Guid Id,
    Guid CampaignId,
    Guid SenderUserId,
    string SenderDisplayName,
    Guid RecipientUserId,
    Guid CharacterId,
    string CharacterName,
    string Body,
    DateTimeOffset SentAt,
    DateTimeOffset? ReadAt)
{
    public static MessageDto From(DirectMessageView view)
    {
        var m = view.Message;
        return new MessageDto(m.Id, m.CampaignId, m.SenderUserId, view.SenderDisplayName, m.RecipientUserId, m.CharacterId, view.CharacterName, m.Body, m.SentAt, m.ReadAt);
    }
}

public sealed record UnreadCountDto(int Count);

/// <param name="CharacterIds">Characters whose players receive the message (one message per character).</param>
/// <param name="Body">Markdown text.</param>
public sealed record SendMessageRequest(IReadOnlyList<Guid> CharacterIds, string Body);

public sealed class SendMessageRequestValidator : AbstractValidator<SendMessageRequest>
{
    public const int MaxRecipients = 50;

    public SendMessageRequestValidator()
    {
        RuleFor(x => x.CharacterIds)
            .NotNull().WithMessage("Indica al menos un personaje.")
            .Must(ids => ids is { Count: > 0 and <= MaxRecipients }).WithMessage($"Indica entre 1 y {MaxRecipients} personajes.")
            .Must(ids => ids is null || ids.All(id => id != Guid.Empty)).WithMessage("Indica personajes válidos.");
        RuleFor(x => x.Body)
            .Must(b => !string.IsNullOrWhiteSpace(b)).WithMessage("El mensaje no puede estar vacío.")
            .Must(b => b is null || b.Trim().Length <= DirectMessage.BodyMaxLength)
            .WithMessage($"El mensaje no puede superar los {DirectMessage.BodyMaxLength} caracteres.");
    }
}

public sealed record ListMessagesQuery(bool? UnreadOnly, int? Limit);

public sealed class ListMessagesQueryValidator : AbstractValidator<ListMessagesQuery>
{
    public const int DefaultLimit = 50;
    public const int MaxLimit = 200;

    public ListMessagesQueryValidator()
    {
        RuleFor(x => x.Limit).InclusiveBetween(1, MaxLimit).WithMessage($"El límite debe estar entre 1 y {MaxLimit}.");
    }
}

public static class MessageErrors
{
    public static AppException MessageNotFound() => AppException.NotFound("Mensaje no encontrado.");
}

/// <summary>
/// A DM sends a secret message to the players of some characters of the campaign: one message per
/// character, addressed to its owner (404 when a character is not in the campaign, 400 when it has no
/// player). Each recipient gets <see cref="CampaignEventTypes.MessageReceived"/>.
/// </summary>
public sealed class SendMessageHandler(
    ICampaignAccess access,
    ICharacterRepository characters,
    IDirectMessageRepository messages,
    IUnitOfWork unitOfWork,
    ICampaignNotifier notifier,
    IDateTimeProvider clock)
{
    public async Task<IReadOnlyList<MessageDto>> HandleAsync(Guid currentUserId, Guid campaignId, SendMessageRequest request, CancellationToken cancellationToken = default)
    {
        await access.RequireAsync(campaignId, currentUserId, CampaignRole.DM, cancellationToken);
        var byId = (await characters.ListByCampaignAsync(campaignId, cancellationToken)).ToDictionary(c => c.Id);
        var now = clock.UtcNow;

        var created = new List<DirectMessage>();
        foreach (var characterId in request.CharacterIds.Distinct())
        {
            var character = byId.GetValueOrDefault(characterId) ?? throw CharacterErrors.CharacterNotFound();
            var recipient = character.OwnerUserId
                ?? throw AppException.Validation("characterIds", $"El personaje '{character.Name}' no tiene jugador al que enviar el mensaje.");
            created.Add(DirectMessage.Create(campaignId, currentUserId, recipient, character.Id, request.Body, now));
        }

        foreach (var message in created)
        {
            messages.Add(message);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        foreach (var message in created)
        {
            await notifier.NotifyUserAsync(
                message.RecipientUserId,
                new CampaignEvent(CampaignEventTypes.MessageReceived, campaignId, message.CharacterId, message.Id, now),
                cancellationToken);
        }

        var views = await messages.ListViewsAsync(created.Select(m => m.Id).ToList(), cancellationToken);
        return views.Select(MessageDto.From).ToList();
    }
}

/// <summary>DMs see the messages they sent; players the ones they received. Newest first.</summary>
public sealed class ListMessagesHandler(ICampaignAccess access, IDirectMessageRepository messages)
{
    public async Task<IReadOnlyList<MessageDto>> HandleAsync(Guid currentUserId, Guid campaignId, ListMessagesQuery query, CancellationToken cancellationToken = default)
    {
        var role = await access.RequireAsync(campaignId, currentUserId, CampaignRole.Player, cancellationToken);
        var unreadOnly = query.UnreadOnly ?? false;
        var limit = query.Limit ?? ListMessagesQueryValidator.DefaultLimit;
        var views = role.IsAtLeast(CampaignRole.DM)
            ? await messages.ListSentAsync(campaignId, currentUserId, unreadOnly, limit, cancellationToken)
            : await messages.ListForRecipientAsync(campaignId, currentUserId, unreadOnly, limit, cancellationToken);
        return views.Select(MessageDto.From).ToList();
    }
}

/// <summary>Unread messages of the user in the campaign (always 0 for DMs, who only send).</summary>
public sealed class GetUnreadCountHandler(ICampaignAccess access, IDirectMessageRepository messages)
{
    public async Task<UnreadCountDto> HandleAsync(Guid currentUserId, Guid campaignId, CancellationToken cancellationToken = default)
    {
        var role = await access.RequireAsync(campaignId, currentUserId, CampaignRole.Player, cancellationToken);
        return role.IsAtLeast(CampaignRole.DM)
            ? new UnreadCountDto(0)
            : new UnreadCountDto(await messages.CountUnreadAsync(campaignId, currentUserId, cancellationToken));
    }
}

/// <summary>The recipient marks a message as read (403 for any other member, 404 for non-members).</summary>
public sealed class MarkMessageReadHandler(
    ICampaignAccess access,
    IDirectMessageRepository messages,
    IUnitOfWork unitOfWork,
    IDateTimeProvider clock)
{
    public async Task<MessageDto> HandleAsync(Guid currentUserId, Guid messageId, CancellationToken cancellationToken = default)
    {
        var message = await messages.GetAsync(messageId, cancellationToken) ?? throw MessageErrors.MessageNotFound();
        if (await access.GetRoleAsync(message.CampaignId, currentUserId, cancellationToken) is null)
        {
            throw MessageErrors.MessageNotFound();
        }

        if (message.RecipientUserId != currentUserId)
        {
            throw AppException.Forbidden("Solo el destinatario puede marcar el mensaje como leído.");
        }

        message.MarkRead(clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return MessageDto.From((await messages.ListViewsAsync([message.Id], cancellationToken)).Single());
    }
}
