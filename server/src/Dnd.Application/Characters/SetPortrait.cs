using Dnd.Application.Abstractions;
using Dnd.Application.Abstractions.Persistence;
using Dnd.Application.Common;
using Dnd.Application.Files;
using Dnd.Domain.Files;

namespace Dnd.Application.Characters;

/// <param name="FileId">A <see cref="FileKind.Portrait"/> file of the character's campaign; null removes the portrait.</param>
public sealed record SetPortraitRequest(Guid? FileId);

/// <summary>The owner of the character or a DM sets (or removes) its portrait; a replaced file is deleted.</summary>
public sealed class SetPortraitHandler(
    CharacterLoader loader,
    IFileRepository files,
    ICharacterSheetService sheets,
    FileCleanup cleanup,
    IUnitOfWork unitOfWork,
    IDateTimeProvider clock)
{
    public async Task<CharacterDetailDto> HandleAsync(Guid currentUserId, Guid characterId, SetPortraitRequest request, CancellationToken cancellationToken = default)
    {
        var loaded = await loader.LoadAsync(characterId, currentUserId, cancellationToken);
        var character = loaded.Character;
        if (!loaded.IsDm && character.OwnerUserId != currentUserId)
        {
            throw AppException.Forbidden("Solo el dueño del personaje o un DM pueden cambiar su retrato.");
        }

        if (request.FileId is { } fileId)
        {
            var file = (await files.ListByIdsAsync([fileId], cancellationToken)).SingleOrDefault();
            if (file is null || file.Kind != FileKind.Portrait || file.CampaignId != character.CampaignId)
            {
                throw AppException.Validation("fileId", "El retrato debe ser una imagen subida como retrato en esta campaña.");
            }
        }

        var previous = character.PortraitFileId;
        character.SetPortrait(request.FileId, clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        if (previous != request.FileId)
        {
            await cleanup.ReleaseAsync([previous], cancellationToken);
        }

        return await sheets.BuildDetailAsync(character, cancellationToken);
    }
}
