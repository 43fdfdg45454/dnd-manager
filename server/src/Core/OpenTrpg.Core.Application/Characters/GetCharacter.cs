using OpenTrpg.Core.Application.Common;

namespace OpenTrpg.Core.Application.Characters;

/// <summary>Full character: owner and DMs only (403 for other members).</summary>
public sealed class GetCharacterHandler(CharacterLoader loader, ICharacterSheetService sheets)
{
    public async Task<CharacterDetailDto> HandleAsync(Guid currentUserId, Guid characterId, CancellationToken cancellationToken = default)
    {
        var loaded = await loader.LoadAsync(characterId, currentUserId, cancellationToken);
        if (!loaded.Character.CanViewSheet(currentUserId, loaded.IsDm))
        {
            throw AppException.Forbidden("Solo el dueño del personaje o un DM pueden ver la hoja completa.");
        }

        return await sheets.BuildDetailAsync(loaded.Character, cancellationToken);
    }
}
