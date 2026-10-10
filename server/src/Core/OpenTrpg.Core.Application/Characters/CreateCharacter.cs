using System.Text.Json.Serialization;
using OpenTrpg.Core.Application.Abstractions;
using OpenTrpg.Core.Application.Abstractions.Persistence;
using OpenTrpg.Core.Application.Common;
using OpenTrpg.Core.Domain.Campaigns;
using OpenTrpg.Core.Domain.Characters;
using FluentValidation;

namespace OpenTrpg.Core.Application.Characters;

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

    /// <summary>Optional height in inches (1–200). No mechanical effect.</summary>
    public int? HeightInches { get; init; }

    /// <summary>Optional weight in pounds (1–2000). No mechanical effect.</summary>
    public int? WeightPounds { get; init; }
}

public sealed class CreateCharacterRequestValidator : AbstractValidator<CreateCharacterRequest>
{
    public CreateCharacterRequestValidator()
    {
        RuleFor(x => x.Name)
            .Must(n => !string.IsNullOrWhiteSpace(n)).WithMessage("Indica el nombre del personaje.")
            .Must(n => n is null || n.Trim().Length <= Character.NameMaxLength)
            .WithMessage($"El nombre no puede superar los {Character.NameMaxLength} caracteres.");
        RuleFor(x => x.HeightInches).InclusiveBetween(Character.MinHeightInches, Character.MaxHeightInches)
            .WithMessage($"La altura debe estar entre {Character.MinHeightInches} y {Character.MaxHeightInches} pulgadas.")
            .When(x => x.HeightInches is not null);
        RuleFor(x => x.WeightPounds).InclusiveBetween(Character.MinWeightPounds, Character.MaxWeightPounds)
            .WithMessage($"El peso debe estar entre {Character.MinWeightPounds} y {Character.MaxWeightPounds} libras.")
            .When(x => x.WeightPounds is not null);
    }
}

/// <summary>Creates a draft (all scores at 10). A player creates their own; a DM creates one for a player or an NPC.</summary>
public sealed class CreateCharacterHandler(
    ICampaignAccess access,
    CharacterOwnerRules ownerRules,
    IDnd5eCharacterRepository characters,
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
        if (request.HeightInches is not null || request.WeightPounds is not null)
        {
            character.SetHeightAndWeight(request.HeightInches, request.WeightPounds, clock.UtcNow);
        }

        var dnd5e = Dnd5eCharacter.Create(character);
        await sheets.RecalculateAsync(dnd5e, cancellationToken);
        characters.Add(dnd5e);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return await sheets.BuildDetailAsync(dnd5e, cancellationToken);
    }
}
