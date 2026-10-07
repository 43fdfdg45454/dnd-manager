using System.Text.Json.Serialization;
using Dnd.Application.Abstractions;
using Dnd.Application.Abstractions.Persistence;
using Dnd.Application.Common;
using Dnd.Domain.Campaigns;
using Dnd.Domain.Characters;
using FluentValidation;

namespace Dnd.Application.Characters;

/// <summary>
/// A player always owns what they create (<see cref="OwnerUserId"/> absent or their own id). A DM has no
/// characters of their own and must send <see cref="OwnerUserId"/>: a player of the campaign, or
/// <c>null</c> for a non-player character without owner.
/// </summary>
public sealed record CreateCharacterRequest
{
    public string Name { get; init; } = string.Empty;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public Optional<Guid?> OwnerUserId { get; init; }
}

public sealed class CreateCharacterRequestValidator : AbstractValidator<CreateCharacterRequest>
{
    public CreateCharacterRequestValidator()
    {
        RuleFor(x => x.Name)
            .Must(n => !string.IsNullOrWhiteSpace(n)).WithMessage("Indica el nombre del personaje.")
            .Must(n => n is null || n.Trim().Length <= Character.NameMaxLength)
            .WithMessage($"El nombre no puede superar los {Character.NameMaxLength} caracteres.");
    }
}

/// <summary>Creates a draft (all scores at 10). A player creates their own; a DM creates one for a player or an NPC.</summary>
public sealed class CreateCharacterHandler(
    ICampaignAccess access,
    CharacterOwnerRules ownerRules,
    ICharacterRepository characters,
    ICharacterSheetService sheets,
    IUnitOfWork unitOfWork,
    IDateTimeProvider clock)
{
    public async Task<CharacterDetailDto> HandleAsync(Guid currentUserId, Guid campaignId, CreateCharacterRequest request, CancellationToken cancellationToken = default)
    {
        var role = await access.RequireAsync(campaignId, currentUserId, CampaignRole.Player, cancellationToken);

        Guid? owner;
        if (!role.IsAtLeast(CampaignRole.DM))
        {
            if (request.OwnerUserId.IsSet && request.OwnerUserId.Value != currentUserId)
            {
                throw AppException.Forbidden("Solo un DM puede crear personajes para otros miembros o personajes sin dueño.");
            }

            owner = currentUserId;
        }
        else
        {
            if (!request.OwnerUserId.IsSet || request.OwnerUserId.Value == currentUserId)
            {
                throw AppException.Validation("ownerUserId", "Un DM no tiene personajes propios: crea un PNJ o asígnalo a un jugador.");
            }

            owner = request.OwnerUserId.Value;
            if (owner is { } ownerId)
            {
                await ownerRules.EnsurePlayerAsync(campaignId, ownerId, cancellationToken);
            }
        }

        var character = Character.Create(campaignId, owner, request.Name, clock.UtcNow);
        await sheets.RecalculateAsync(character, cancellationToken);
        characters.Add(character);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return await sheets.BuildDetailAsync(character, cancellationToken);
    }
}
