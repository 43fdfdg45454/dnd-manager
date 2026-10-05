using Dnd.Application.Abstractions;
using Dnd.Application.Abstractions.Persistence;
using Dnd.Application.ChangeRequests;
using Dnd.Domain.Characters;

namespace Dnd.Application.Characters;

/// <summary>Outcome of a sheet edit: applied (<see cref="Character"/>) or sent to the DM (<see cref="ChangeRequest"/>).</summary>
public sealed record SheetPatchResult(CharacterDetailDto? Character, ChangeRequestDto? ChangeRequest);

/// <summary>
/// Edits the sheet. DMs, and the owner of a draft, edit directly; the owner of an active character
/// creates an EditSheet change request with the patch as payload.
/// </summary>
public sealed class UpdateSheetHandler(
    CharacterLoader loader,
    ICharacterSheetService sheets,
    IChangeRequestRepository changeRequests,
    IUnitOfWork unitOfWork,
    IDateTimeProvider clock)
{
    public async Task<SheetPatchResult> HandleAsync(Guid currentUserId, Guid characterId, SheetPatch patch, CancellationToken cancellationToken = default)
    {
        var loaded = await loader.LoadAsync(characterId, currentUserId, cancellationToken);
        var character = loaded.Character;
        var mode = character.ResolveSheetEdit(currentUserId, loaded.IsDm);

        var edit = patch.ToSheetEdit();
        await sheets.EnsureCatalogReferencesAsync(character, edit, cancellationToken);

        if (mode == SheetEditMode.Direct)
        {
            character.ApplySheetEdit(edit, clock.UtcNow);
            await sheets.RecalculateAsync(character, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return new SheetPatchResult(await sheets.BuildDetailAsync(character, cancellationToken), null);
        }

        var request = ChangeRequest.Create(
            character.CampaignId,
            character.Id,
            currentUserId,
            ChangeRequestType.EditSheet,
            SheetPatchJson.Serialize(patch),
            clock.UtcNow);
        changeRequests.Add(request);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var view = (await changeRequests.ListViewsAsync(new ChangeRequestQuery(Id: request.Id), cancellationToken)).Single();
        return new SheetPatchResult(null, ChangeRequestDto.From(view));
    }
}
