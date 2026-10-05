using Dnd.Application.Abstractions;
using Dnd.Application.Abstractions.Persistence;
using Dnd.Application.Common;
using Dnd.Domain.Campaigns;
using Dnd.Domain.Characters;

namespace Dnd.Application.Characters;

public static class CharacterErrors
{
    /// <summary>Also used for users who are not members of the character's campaign.</summary>
    public static AppException CharacterNotFound() => AppException.NotFound("Personaje no encontrado.");

    public static AppException ChangeRequestNotFound() => AppException.NotFound("Solicitud no encontrada.");
}

/// <summary>A character loaded for a use case, with the role of the acting user in its campaign.</summary>
public sealed record LoadedCharacter(Character Character, CampaignRole Role)
{
    public bool IsDm => Role.IsAtLeast(CampaignRole.DM);
}

/// <summary>Loads a character (tracked, with every child collection) and resolves the actor's campaign role.</summary>
public sealed class CharacterLoader(ICharacterRepository characters, ICampaignAccess access)
{
    /// <summary>404 when the character does not exist or the actor is not a member of its campaign.</summary>
    public async Task<LoadedCharacter> LoadAsync(Guid characterId, Guid actorUserId, CancellationToken cancellationToken = default)
    {
        var character = await characters.GetWithDetailsAsync(characterId, cancellationToken) ?? throw CharacterErrors.CharacterNotFound();
        var role = await access.GetRoleAsync(character.CampaignId, actorUserId, cancellationToken) ?? throw CharacterErrors.CharacterNotFound();
        return new LoadedCharacter(character, role);
    }
}
