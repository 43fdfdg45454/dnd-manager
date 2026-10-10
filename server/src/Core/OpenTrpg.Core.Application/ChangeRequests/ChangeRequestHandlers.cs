using OpenTrpg.Core.Application.Abstractions;
using OpenTrpg.Core.Application.Abstractions.Persistence;
using OpenTrpg.Core.Application.Characters;
using OpenTrpg.Core.Application.Common;
using OpenTrpg.Core.Application.Items;
using OpenTrpg.Core.Domain.Campaigns;
using OpenTrpg.Core.Domain.Characters;
using FluentValidation;

namespace OpenTrpg.Core.Application.ChangeRequests;

/// <summary>
/// Change requests of a campaign, newest first: DMs see all of them, players only their own.
/// <paramref name="status"/> filters by status name (Pending, Approved, Rejected, Cancelled).
/// </summary>
public sealed class ListChangeRequestsHandler(ICampaignAccess access, IChangeRequestRepository changeRequests)
{
    public async Task<IReadOnlyList<ChangeRequestDto>> HandleAsync(Guid currentUserId, Guid campaignId, string? status, CancellationToken cancellationToken = default)
    {
        var role = await access.RequireAsync(campaignId, currentUserId, CampaignRole.Player, cancellationToken);

        ChangeRequestStatus? statusFilter = null;
        if (!string.IsNullOrEmpty(status))
        {
            statusFilter = EnumNames.TryParse<ChangeRequestStatus>(status, out var parsed)
                ? parsed
                : throw AppException.Validation("status", $"El estado debe ser {EnumNames.Describe<ChangeRequestStatus>()}.");
        }

        var views = await changeRequests.ListViewsAsync(
            new ChangeRequestQuery(
                CampaignId: campaignId,
                RequestedByUserId: role.IsAtLeast(CampaignRole.DM) ? null : currentUserId,
                Status: statusFilter),
            cancellationToken);
        return views.Select(ChangeRequestDto.From).ToList();
    }
}

/// <summary>Loads a change request with the actor's role in its campaign (404 for non-members).</summary>
public sealed class ChangeRequestLoader(IChangeRequestRepository changeRequests, ICampaignAccess access)
{
    public async Task<(ChangeRequestView View, CampaignRole Role)> LoadViewAsync(Guid requestId, Guid actorUserId, CancellationToken cancellationToken)
    {
        var view = (await changeRequests.ListViewsAsync(new ChangeRequestQuery(Id: requestId), cancellationToken)).SingleOrDefault()
            ?? throw CharacterErrors.ChangeRequestNotFound();
        var role = await access.GetRoleAsync(view.Request.CampaignId, actorUserId, cancellationToken)
            ?? throw CharacterErrors.ChangeRequestNotFound();
        return (view, role);
    }

    /// <summary>Tracked request, for resolving it.</summary>
    public async Task<(ChangeRequest Request, CampaignRole Role)> LoadAsync(Guid requestId, Guid actorUserId, CancellationToken cancellationToken)
    {
        var request = await changeRequests.GetByIdAsync(requestId, cancellationToken) ?? throw CharacterErrors.ChangeRequestNotFound();
        var role = await access.GetRoleAsync(request.CampaignId, actorUserId, cancellationToken)
            ?? throw CharacterErrors.ChangeRequestNotFound();
        return (request, role);
    }

    public async Task<ChangeRequestDto> ToDtoAsync(Guid requestId, CancellationToken cancellationToken) =>
        ChangeRequestDto.From((await changeRequests.ListViewsAsync(new ChangeRequestQuery(Id: requestId), cancellationToken)).Single());
}

/// <summary>A change request: DMs, its requester and the character's owner.</summary>
public sealed class GetChangeRequestHandler(ChangeRequestLoader loader)
{
    public async Task<ChangeRequestDto> HandleAsync(Guid currentUserId, Guid requestId, CancellationToken cancellationToken = default)
    {
        var (view, role) = await loader.LoadViewAsync(requestId, currentUserId, cancellationToken);
        if (!role.IsAtLeast(CampaignRole.DM) && view.Request.RequestedByUserId != currentUserId && view.CharacterOwnerUserId != currentUserId)
        {
            throw AppException.Forbidden("No puedes ver esta solicitud.");
        }

        return ChangeRequestDto.From(view);
    }
}

public sealed record ApproveChangeRequestRequest(string? Comment);

public sealed class ApproveChangeRequestRequestValidator : AbstractValidator<ApproveChangeRequestRequest>
{
    public ApproveChangeRequestRequestValidator()
    {
        RuleFor(x => x.Comment).MaximumLength(ChangeRequest.CommentMaxLength)
            .WithMessage($"El comentario no puede superar los {ChangeRequest.CommentMaxLength} caracteres.");
    }
}

/// <summary>
/// A DM approves a pending request and its payload is applied in the same transaction, with the
/// same logic as a direct edit. "Other" requests carry nothing to apply (409).
/// </summary>
public sealed class ApproveChangeRequestHandler(
    ChangeRequestLoader loader,
    ICharacterRepository characters,
    ICharacterSheetService sheets,
    SpellPreparationPlanner preparation,
    OriginChoicesPlanner originChoices,
    IValidator<SheetPatch> patchValidator,
    InventoryOperations inventory,
    CompanionPlanner companions,
    IUnitOfWork unitOfWork,
    ICampaignNotifier notifier,
    IDateTimeProvider clock)
{
    public async Task<ChangeRequestDto> HandleAsync(Guid currentUserId, Guid requestId, ApproveChangeRequestRequest? body, CancellationToken cancellationToken = default)
    {
        var (request, role) = await loader.LoadAsync(requestId, currentUserId, cancellationToken);
        if (!role.IsAtLeast(CampaignRole.DM))
        {
            throw AppException.Forbidden("Solo un DM puede aprobar solicitudes.");
        }

        if (!request.IsPending)
        {
            throw AppException.Conflict("La solicitud ya no está pendiente.");
        }

        var character = await characters.GetWithDetailsAsync(request.CharacterId, cancellationToken) ?? throw CharacterErrors.CharacterNotFound();
        var now = clock.UtcNow;
        switch (request.Type)
        {
            case ChangeRequestType.Activate:
                if (character.Status == CharacterStatus.Draft)
                {
                    await originChoices.EnsureCompleteAsync(character, cancellationToken);
                }

                var sheet = await sheets.CalculateAsync(character, cancellationToken);
                character.Activate(sheet.HitPointsMax, now);
                await preparation.RequireInitialPreparationAsync(character, now, cancellationToken);
                break;
            case ChangeRequestType.EditSheet:
                await ApplySheetPatchAsync(character, request.PayloadJson, now, cancellationToken);
                break;
            case ChangeRequestType.AddItem or ChangeRequestType.CustomItem or ChangeRequestType.RemoveItem or ChangeRequestType.AdjustMoney:
                await inventory.ApplyApprovedAsync(character, request, now, cancellationToken);

                // Removing an equipped item can lower the sheet (item modifiers): cap the current hit points.
                await sheets.RecalculateAsync(character, cancellationToken);
                break;
            case ChangeRequestType.Companion:
                await companions.ApplyApprovedAsync(character, request.PayloadJson, now, cancellationToken);
                break;
            default:
                throw AppException.Conflict("Este tipo de solicitud todavía no está soportado.");
        }

        request.Approve(currentUserId, body?.Comment, now);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await notifier.ChangeRequestUpdatedAsync(request.CampaignId, request.CharacterId, request.Id, now, cancellationToken);
        await notifier.CharacterUpdatedAsync(request.CampaignId, request.CharacterId, now, cancellationToken);
        await notifier.ChangeRequestResolvedAsync(request.RequestedByUserId, request.CampaignId, request.CharacterId, request.Id, now, cancellationToken);
        return await loader.ToDtoAsync(request.Id, cancellationToken);
    }

    private async Task ApplySheetPatchAsync(Character character, string payloadJson, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var patch = SheetPatchJson.TryDeserialize(payloadJson)
            ?? throw AppException.Validation("payload", "El contenido de la solicitud no es una edición de hoja válida.");
        var validation = await patchValidator.ValidateAsync(patch, cancellationToken);
        if (!validation.IsValid)
        {
            throw AppException.Validation("payload", validation.Errors[0].ErrorMessage);
        }

        // Sheet edit requests come from the owner of an active character, who cannot prepare spells this way.
        var edit = patch.ToSheetEdit() with { KeepSpellPreparation = true };
        await sheets.EnsureCatalogReferencesAsync(character, edit, cancellationToken);
        character.ApplySheetEdit(edit, now);
        await sheets.RecalculateAsync(character, cancellationToken);
    }
}

public sealed record RejectChangeRequestRequest(string Comment);

public sealed class RejectChangeRequestRequestValidator : AbstractValidator<RejectChangeRequestRequest>
{
    public RejectChangeRequestRequestValidator()
    {
        RuleFor(x => x.Comment)
            .Must(c => !string.IsNullOrWhiteSpace(c)).WithMessage("Hay que indicar el motivo del rechazo.")
            .MaximumLength(ChangeRequest.CommentMaxLength)
            .WithMessage($"El comentario no puede superar los {ChangeRequest.CommentMaxLength} caracteres.");
    }
}

/// <summary>A DM rejects a pending request with a comment.</summary>
public sealed class RejectChangeRequestHandler(ChangeRequestLoader loader, IUnitOfWork unitOfWork, ICampaignNotifier notifier, IDateTimeProvider clock)
{
    public async Task<ChangeRequestDto> HandleAsync(Guid currentUserId, Guid requestId, RejectChangeRequestRequest body, CancellationToken cancellationToken = default)
    {
        var (request, role) = await loader.LoadAsync(requestId, currentUserId, cancellationToken);
        if (!role.IsAtLeast(CampaignRole.DM))
        {
            throw AppException.Forbidden("Solo un DM puede rechazar solicitudes.");
        }

        request.Reject(currentUserId, body.Comment, clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await notifier.ChangeRequestUpdatedAsync(request.CampaignId, request.CharacterId, request.Id, clock.UtcNow, cancellationToken);
        await notifier.ChangeRequestResolvedAsync(request.RequestedByUserId, request.CampaignId, request.CharacterId, request.Id, clock.UtcNow, cancellationToken);
        return await loader.ToDtoAsync(request.Id, cancellationToken);
    }
}

/// <summary>The requester withdraws a pending request.</summary>
public sealed class CancelChangeRequestHandler(ChangeRequestLoader loader, IUnitOfWork unitOfWork, ICampaignNotifier notifier, IDateTimeProvider clock)
{
    public async Task<ChangeRequestDto> HandleAsync(Guid currentUserId, Guid requestId, CancellationToken cancellationToken = default)
    {
        var (request, _) = await loader.LoadAsync(requestId, currentUserId, cancellationToken);
        request.Cancel(currentUserId, clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await notifier.ChangeRequestUpdatedAsync(request.CampaignId, request.CharacterId, request.Id, clock.UtcNow, cancellationToken);
        return await loader.ToDtoAsync(request.Id, cancellationToken);
    }
}
