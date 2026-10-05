using System.Text.Json.Serialization;
using Dnd.Application.Abstractions;
using Dnd.Application.Abstractions.Persistence;
using Dnd.Application.Common;
using Dnd.Domain.Campaigns;
using Dnd.Domain.Characters;
using FluentValidation;

namespace Dnd.Application.Characters;

/// <summary>
/// <see cref="OwnerUserId"/> absent: the creator owns the character. Present (DM only, unless it is
/// the creator): a member of the campaign, or <c>null</c> for a non-player character without owner.
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

/// <summary>Creates a draft (all scores at 10). Any member creates their own; a DM can create one for another member or an NPC.</summary>
public sealed class CreateCharacterHandler(
    ICampaignAccess access,
    ICharacterRepository characters,
    ICharacterSheetService sheets,
    IUnitOfWork unitOfWork,
    IDateTimeProvider clock)
{
    public async Task<CharacterDetailDto> HandleAsync(Guid currentUserId, Guid campaignId, CreateCharacterRequest request, CancellationToken cancellationToken = default)
    {
        var role = await access.RequireAsync(campaignId, currentUserId, CampaignRole.Player, cancellationToken);

        Guid? owner = currentUserId;
        if (request.OwnerUserId.IsSet && request.OwnerUserId.Value != currentUserId)
        {
            if (!role.IsAtLeast(CampaignRole.DM))
            {
                throw AppException.Forbidden("Solo un DM puede crear personajes para otros miembros o personajes sin dueño.");
            }

            owner = request.OwnerUserId.Value;
            if (owner is { } ownerId && await access.GetRoleAsync(campaignId, ownerId, cancellationToken) is null)
            {
                throw AppException.Validation("ownerUserId", "El dueño debe ser miembro de la campaña.");
            }
        }

        var character = Character.Create(campaignId, owner, request.Name, clock.UtcNow);
        await sheets.RecalculateAsync(character, cancellationToken);
        characters.Add(character);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return await sheets.BuildDetailAsync(character, cancellationToken);
    }
}
