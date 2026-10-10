using OpenTrpg.Core.Application.Abstractions;
using OpenTrpg.Core.Application.Abstractions.Persistence;
using OpenTrpg.Core.Application.Common;
using OpenTrpg.Core.Domain.Campaigns;

namespace OpenTrpg.Core.Application.Characters;

/// <summary>
/// Invariant of character ownership: a character with an owner belongs to a member whose role in the
/// campaign is <see cref="CampaignRole.Player"/>. DMs and the owner of the campaign only have
/// non-player characters (without owner).
/// </summary>
public sealed class CharacterOwnerRules(ICampaignAccess access, ICharacterRepository characters)
{
    /// <summary>400 on <c>ownerUserId</c> unless the user is a player of the campaign.</summary>
    public async Task EnsurePlayerAsync(Guid campaignId, Guid ownerUserId, CancellationToken cancellationToken = default)
    {
        var role = await access.GetRoleAsync(campaignId, ownerUserId, cancellationToken)
            ?? throw AppException.Validation("ownerUserId", "El dueño debe ser miembro de la campaña.");
        if (role != CampaignRole.Player)
        {
            throw AppException.Validation("ownerUserId", "El dueño debe ser un jugador de la campaña.");
        }
    }

    /// <summary>409 when the member about to become DM or owner still owns characters in the campaign.</summary>
    public async Task EnsureOwnsNoCharactersAsync(Guid campaignId, Guid userId, string displayName, CancellationToken cancellationToken = default)
    {
        var names = await characters.ListNamesOwnedByAsync(campaignId, userId, cancellationToken);
        if (names.Count > 0)
        {
            throw AppException.Conflict(
                $"{displayName} tiene personajes en la campaña ({string.Join(", ", names)}). Reasígnalos o conviértelos en PNJ antes de nombrarlo DM.",
                "member-owns-characters");
        }
    }
}

/// <param name="OwnerUserId">A player of the campaign, or <c>null</c> to make it a non-player character. Required.</param>
public sealed record SetCharacterOwnerRequest
{
    public Optional<Guid?> OwnerUserId { get; init; }
}

/// <summary>A DM hands a character to a player of the campaign or makes it a non-player character, without approval.</summary>
public sealed class SetCharacterOwnerHandler(
    CharacterLoader loader,
    CharacterOwnerRules rules,
    CharacterViews views,
    IUnitOfWork unitOfWork,
    ICampaignNotifier notifier,
    IDateTimeProvider clock)
{
    public async Task<CharacterDetailDto> HandleAsync(Guid currentUserId, Guid characterId, SetCharacterOwnerRequest request, CancellationToken cancellationToken = default)
    {
        var loaded = await loader.LoadAsync(characterId, currentUserId, cancellationToken);
        if (!loaded.IsDm)
        {
            throw AppException.Forbidden("Solo un DM puede cambiar el jugador de un personaje.");
        }

        if (!request.OwnerUserId.IsSet)
        {
            throw AppException.Validation("ownerUserId", "Indica el jugador, o null para convertirlo en PNJ.");
        }

        var character = loaded.Character;
        if (request.OwnerUserId.Value is { } ownerId)
        {
            await rules.EnsurePlayerAsync(character.CampaignId, ownerId, cancellationToken);
        }

        character.ChangeOwner(request.OwnerUserId.Value, clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await notifier.CharacterUpdatedAsync(character.CampaignId, character.Id, clock.UtcNow, cancellationToken);
        return await views.BuildDetailAsync(character, cancellationToken);
    }
}
