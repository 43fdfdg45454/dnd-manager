using Dnd.Application.Abstractions;
using Dnd.Application.Abstractions.Persistence;
using Dnd.Application.ChangeRequests;
using Dnd.Application.Common;
using Dnd.Domain.Characters;

namespace Dnd.Application.Characters;

/// <summary>The owner of a draft asks the DMs to activate it (an Activate change request).</summary>
public sealed class SubmitCharacterHandler(
    CharacterLoader loader,
    IChangeRequestRepository changeRequests,
    IUnitOfWork unitOfWork,
    ICampaignNotifier notifier,
    IDateTimeProvider clock)
{
    public async Task<ChangeRequestDto> HandleAsync(Guid currentUserId, Guid characterId, CancellationToken cancellationToken = default)
    {
        var character = (await loader.LoadAsync(characterId, currentUserId, cancellationToken)).Character;
        character.EnsureCanSubmit(currentUserId);

        if ((await changeRequests.ListPendingAsync(character.Id, ChangeRequestType.Activate, cancellationToken)).Count > 0)
        {
            throw AppException.Conflict("Ya hay una solicitud de activación pendiente para este personaje.");
        }

        var request = ChangeRequest.Create(character.CampaignId, character.Id, currentUserId, ChangeRequestType.Activate, null, clock.UtcNow);
        changeRequests.Add(request);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await notifier.ChangeRequestUpdatedAsync(character.CampaignId, character.Id, request.Id, clock.UtcNow, cancellationToken);

        var view = (await changeRequests.ListViewsAsync(new ChangeRequestQuery(Id: request.Id), cancellationToken)).Single();
        return ChangeRequestDto.From(view);
    }
}

/// <summary>
/// A DM activates a draft directly; it enters play at full hit points. Pending Activate requests of
/// the character are marked approved by the same DM.
/// </summary>
public sealed class ActivateCharacterHandler(
    CharacterLoader loader,
    ICharacterSheetService sheets,
    IChangeRequestRepository changeRequests,
    IUnitOfWork unitOfWork,
    ICampaignNotifier notifier,
    IDateTimeProvider clock)
{
    public async Task<CharacterDetailDto> HandleAsync(Guid currentUserId, Guid characterId, CancellationToken cancellationToken = default)
    {
        var loaded = await loader.LoadAsync(characterId, currentUserId, cancellationToken);
        if (!loaded.IsDm)
        {
            throw AppException.Forbidden("Solo un DM puede activar un personaje directamente.");
        }

        var character = loaded.Character;
        var now = clock.UtcNow;
        var sheet = await sheets.CalculateAsync(character, cancellationToken);
        character.Activate(sheet.HitPointsMax, now);

        foreach (var pending in await changeRequests.ListPendingAsync(character.Id, ChangeRequestType.Activate, cancellationToken))
        {
            pending.Approve(currentUserId, "Activado directamente por un DM.", now);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await notifier.CharacterUpdatedAsync(character.CampaignId, character.Id, clock.UtcNow, cancellationToken);
        return await sheets.BuildDetailAsync(character, cancellationToken);
    }
}

/// <summary>DMs delete any character; the owner only a draft. Change requests go with it.</summary>
public sealed class DeleteCharacterHandler(CharacterLoader loader, ICharacterRepository characters, IUnitOfWork unitOfWork)
{
    public async Task HandleAsync(Guid currentUserId, Guid characterId, CancellationToken cancellationToken = default)
    {
        var loaded = await loader.LoadAsync(characterId, currentUserId, cancellationToken);
        loaded.Character.EnsureCanDelete(currentUserId, loaded.IsDm);
        characters.Remove(loaded.Character);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
