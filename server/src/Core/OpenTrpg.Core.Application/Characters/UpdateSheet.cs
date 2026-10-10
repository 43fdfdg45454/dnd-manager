using System.Text.Json;
using System.Text.Json.Nodes;
using OpenTrpg.Core.Application.Abstractions;
using OpenTrpg.Core.Application.Abstractions.Persistence;
using OpenTrpg.Core.Application.ChangeRequests;
using OpenTrpg.Core.Application.Common;
using OpenTrpg.Core.Application.Systems;
using OpenTrpg.Core.Domain.Characters;

namespace OpenTrpg.Core.Application.Characters;

/// <summary>Outcome of a sheet edit: applied (<see cref="Character"/>) or sent to the DM (<see cref="ChangeRequest"/>).</summary>
public sealed record SheetPatchResult(CharacterDetailDto? Character, ChangeRequestDto? ChangeRequest);

/// <summary>
/// Edits the sheet with the game system's patch (which includes the core profile fields). DMs, and the owner of a
/// draft, edit directly; the owner of an active character creates a change request of the system's edit type with
/// the patch as payload. Height and weight have no mechanical effect, so they are always applied directly (only the
/// rest of the patch goes to the DM).
/// </summary>
public sealed class UpdateSheetHandler(
    CharacterLoader loader,
    CampaignSystems systems,
    CharacterViews views,
    IChangeRequestRepository changeRequests,
    IUnitOfWork unitOfWork,
    ICampaignNotifier notifier,
    IDateTimeProvider clock)
{
    public async Task<SheetPatchResult> HandleAsync(Guid currentUserId, Guid characterId, JsonElement patch, CancellationToken cancellationToken = default)
    {
        var loaded = await loader.LoadAsync(characterId, currentUserId, cancellationToken);
        var character = loaded.Character;
        var reference = new CharacterRef(character);
        var mode = character.ResolveSheetEdit(currentUserId, loaded.IsDm);
        var system = await systems.ForCharacterAsync(character, cancellationToken);
        var normalized = await system.Sheets.ValidateEditAsync(reference, patch, cancellationToken);

        if (mode == SheetEditMode.Direct)
        {
            await system.Sheets.ApplyEditAsync(reference, patch, clock.UtcNow, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await notifier.CharacterUpdatedAsync(character.CampaignId, character.Id, clock.UtcNow, cancellationToken);
            return new SheetPatchResult(await views.BuildDetailAsync(reference, cancellationToken), null);
        }

        var profile = normalized.Deserialize<CharacterProfilePatch>(JsonSerializerOptions.Web) ?? new CharacterProfilePatch();
        var appliedHeightOrWeight = profile.HasHeightOrWeight;
        if (appliedHeightOrWeight)
        {
            var edit = profile.ToProfileEdit();
            character.SetHeightAndWeight(edit.HeightInches, edit.WeightPounds, clock.UtcNow);
            foreach (var field in CharacterProfilePatch.HeightAndWeightFields)
            {
                normalized.Remove(field);
            }

            if (normalized.Count == 0)
            {
                await unitOfWork.SaveChangesAsync(cancellationToken);
                await notifier.CharacterUpdatedAsync(character.CampaignId, character.Id, clock.UtcNow, cancellationToken);
                return new SheetPatchResult(await views.BuildDetailAsync(reference, cancellationToken), null);
            }
        }

        var payload = JsonSerializer.SerializeToElement(normalized);
        var before = await system.Sheets.SnapshotAsync(reference, payload, cancellationToken);
        var request = ChangeRequest.Create(
            character.CampaignId,
            character.Id,
            currentUserId,
            system.Sheets.EditRequestType,
            normalized.ToJsonString(),
            clock.UtcNow,
            before.ToJsonString());
        changeRequests.Add(request);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        if (appliedHeightOrWeight)
        {
            await notifier.CharacterUpdatedAsync(character.CampaignId, character.Id, clock.UtcNow, cancellationToken);
        }

        await notifier.ChangeRequestUpdatedAsync(character.CampaignId, character.Id, request.Id, clock.UtcNow, cancellationToken);

        var view = (await changeRequests.ListViewsAsync(new ChangeRequestQuery(Id: request.Id), cancellationToken)).Single();
        return new SheetPatchResult(null, ChangeRequestDto.From(view));
    }
}
