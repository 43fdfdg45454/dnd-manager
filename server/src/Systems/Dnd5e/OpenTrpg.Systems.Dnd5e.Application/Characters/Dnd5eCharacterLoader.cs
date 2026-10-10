using OpenTrpg.Core.Application.Abstractions;
using OpenTrpg.Core.Application.Abstractions.Persistence;
using OpenTrpg.Core.Domain.Campaigns;
using OpenTrpg.Core.Domain.Characters;
using OpenTrpg.Core.Application;
using OpenTrpg.Core.Application.Characters;
using OpenTrpg.Systems.Dnd5e.Application;
using OpenTrpg.Systems.Dnd5e.Application.Abstractions;
using OpenTrpg.Systems.Dnd5e.Application.Abstractions.Persistence;
using OpenTrpg.Systems.Dnd5e.Application.Characters;
using OpenTrpg.Systems.Dnd5e.Domain.Characters;

namespace OpenTrpg.Systems.Dnd5e.Application.Characters;

/// <summary>A D&amp;D 5e character loaded for a use case, with the role of the acting user in its campaign.</summary>
public sealed record LoadedDnd5eCharacter(Dnd5eCharacter Character, CampaignRole Role)
{
    public bool IsDm => Role.IsAtLeast(CampaignRole.DM);
}

/// <summary>
/// Loads a D&amp;D 5e character (tracked, with every child collection and its core character) and resolves the
/// actor's campaign role, like <see cref="CharacterLoader"/> does for the core character.
/// </summary>
public sealed class Dnd5eCharacterLoader(IDnd5eCharacterRepository characters, ICampaignAccess access)
{
    /// <summary>404 when the character does not exist or the actor is not a member of its campaign.</summary>
    public async Task<LoadedDnd5eCharacter> LoadAsync(Guid characterId, Guid actorUserId, CancellationToken cancellationToken = default)
    {
        var character = await characters.GetWithDetailsAsync(characterId, cancellationToken) ?? throw CharacterErrors.CharacterNotFound();
        var role = await access.GetRoleAsync(character.CampaignId, actorUserId, cancellationToken) ?? throw CharacterErrors.CharacterNotFound();
        return new LoadedDnd5eCharacter(character, role);
    }

    /// <summary>The 5e part of a core character already loaded (tracked); 404 when it has none.</summary>
    public async Task<Dnd5eCharacter> LoadAsync(Character character, CancellationToken cancellationToken = default) =>
        await characters.GetWithDetailsAsync(character.Id, cancellationToken) ?? throw CharacterErrors.CharacterNotFound();
}
