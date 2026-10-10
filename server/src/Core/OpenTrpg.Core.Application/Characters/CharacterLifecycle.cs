using OpenTrpg.Core.Application.Abstractions;
using OpenTrpg.Core.Application.Abstractions.Persistence;
using OpenTrpg.Core.Application.ChangeRequests;
using OpenTrpg.Core.Application.Common;
using OpenTrpg.Core.Application.Systems;
using OpenTrpg.Core.Domain.Characters;

namespace OpenTrpg.Core.Application.Characters;

/// <summary>
/// Activation of a character (a DM directly or by approving an Activate request): the game system checks that a
/// draft is ready, the core changes its status and the system prepares its part (in D&amp;D 5e, full hit points and
/// the initial spell preparation) in the same unit of work.
/// </summary>
public sealed class CharacterActivation
{
    public async Task ActivateAsync(CharacterRef character, IGameSystem system, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (character.Character.Status == CharacterStatus.Draft)
        {
            await system.Creation.EnsureReadyForActivationAsync(character, cancellationToken);
        }

        character.Character.Activate(now);
        await system.Creation.PrepareActivationAsync(character, now, cancellationToken);
    }
}

/// <summary>The owner of a draft asks the DMs to activate it (an Activate change request).</summary>
public sealed class SubmitCharacterHandler(
    CharacterLoader loader,
    CampaignSystems systems,
    IChangeRequestRepository changeRequests,
    IUnitOfWork unitOfWork,
    ICampaignNotifier notifier,
    IDateTimeProvider clock)
{
    public async Task<ChangeRequestDto> HandleAsync(Guid currentUserId, Guid characterId, CancellationToken cancellationToken = default)
    {
        var character = (await loader.LoadAsync(characterId, currentUserId, cancellationToken)).Character;
        character.EnsureCanSubmit(currentUserId);
        var system = await systems.ForCharacterAsync(character, cancellationToken);
        await system.Creation.EnsureReadyForActivationAsync(new CharacterRef(character), cancellationToken);

        if ((await changeRequests.ListPendingAsync(character.Id, ChangeRequestTypes.Activate, cancellationToken)).Count > 0)
        {
            throw AppException.Conflict("Ya hay una solicitud de activación pendiente para este personaje.");
        }

        var request = ChangeRequest.Create(character.CampaignId, character.Id, currentUserId, ChangeRequestTypes.Activate, null, clock.UtcNow);
        changeRequests.Add(request);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await notifier.ChangeRequestUpdatedAsync(character.CampaignId, character.Id, request.Id, clock.UtcNow, cancellationToken);

        var view = (await changeRequests.ListViewsAsync(new ChangeRequestQuery(Id: request.Id), cancellationToken)).Single();
        return ChangeRequestDto.From(view);
    }
}

/// <summary>
/// A DM activates a draft directly (see <see cref="CharacterActivation"/>). Pending Activate requests of the character
/// are marked approved by the same DM.
/// </summary>
public sealed class ActivateCharacterHandler(
    CharacterLoader loader,
    CampaignSystems systems,
    CharacterActivation activation,
    CharacterViews views,
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
        var reference = new CharacterRef(character);
        var system = await systems.ForCharacterAsync(character, cancellationToken);
        var now = clock.UtcNow;
        await activation.ActivateAsync(reference, system, now, cancellationToken);

        foreach (var pending in await changeRequests.ListPendingAsync(character.Id, ChangeRequestTypes.Activate, cancellationToken))
        {
            pending.Approve(currentUserId, "Activado directamente por un DM.", now);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await notifier.CharacterUpdatedAsync(character.CampaignId, character.Id, clock.UtcNow, cancellationToken);
        return await views.BuildDetailAsync(reference, cancellationToken);
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
