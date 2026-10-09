using Dnd.Application.Abstractions;
using Dnd.Application.Abstractions.Persistence;
using Dnd.Application.Common;
using Dnd.Domain.Campaigns;
using FluentValidation;

namespace Dnd.Application.Campaigns;

/// <param name="Role">"DM" or "Player".</param>
public sealed record AddMemberRequest(Guid UserId, string Role);

public sealed class AddMemberRequestValidator : AbstractValidator<AddMemberRequest>
{
    public AddMemberRequestValidator()
    {
        RuleFor(x => x.UserId).NotEmpty().WithMessage("Indica el usuario.");
        RuleFor(x => x.Role).Must(CampaignRoles.IsAssignable).WithMessage(CampaignRoles.AssignableMessage);
    }
}

/// <summary>Pending invitation as the DMs of the campaign see it.</summary>
public sealed record CampaignInvitationDto(Guid Id, Guid UserId, string DisplayName, string Email, string Role, string InvitedByDisplayName, DateTimeOffset CreatedAt);

/// <summary>Pending invitation as the invited user sees it.</summary>
public sealed record MyInvitationDto(Guid Id, Guid CampaignId, string CampaignName, string Role, string InvitedByDisplayName, DateTimeOffset CreatedAt);

internal static class InvitationErrors
{
    public static AppException NotFound() => AppException.NotFound("Invitación no encontrada.");
}

/// <summary>
/// Invites an active user to the campaign. At least DM invites players; only the owner invites DMs.
/// The user becomes a member when they accept (<see cref="AcceptInvitationHandler"/>).
/// </summary>
public sealed class InviteMemberHandler(
    ICampaignAccess access,
    ICampaignRepository campaigns,
    IUserRepository users,
    IUnitOfWork unitOfWork,
    ICampaignNotifier notifier,
    IDateTimeProvider clock)
{
    public async Task<CampaignInvitationDto> HandleAsync(Guid currentUserId, Guid campaignId, AddMemberRequest request, CancellationToken cancellationToken = default)
    {
        await access.RequireAsync(campaignId, currentUserId, CampaignRole.DM, cancellationToken);

        var user = await users.GetByIdAsync(request.UserId, cancellationToken);
        if (user is null || !user.IsActive)
        {
            throw AppException.NotFound("El usuario no existe o está desactivado.");
        }

        var campaign = await campaigns.GetWithMembersAsync(campaignId, cancellationToken) ?? throw CampaignErrors.CampaignNotFound();
        var invitation = campaign.Invite(currentUserId, user.Id, CampaignRoles.ParseAssignable(request.Role), clock.UtcNow);
        if (await campaigns.FindInvitationAsync(campaignId, user.Id, cancellationToken) is not null)
        {
            throw AppException.Conflict("El usuario ya tiene una invitación pendiente a esta campaña.");
        }

        campaigns.AddInvitation(invitation);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await notifier.InvitationReceivedAsync(campaignId, user.Id, invitation.Id, clock.UtcNow, cancellationToken);
        await notifier.MembersUpdatedAsync(campaignId, clock.UtcNow, cancellationToken);

        var inviter = await users.GetByIdAsync(currentUserId, cancellationToken);
        return new CampaignInvitationDto(
            invitation.Id, user.Id, user.DisplayName, user.Email, invitation.Role.ToString(), inviter?.DisplayName ?? string.Empty, invitation.CreatedAt);
    }
}

/// <summary>Pending invitations of a campaign. At least DM.</summary>
public sealed class ListCampaignInvitationsHandler(ICampaignAccess access, ICampaignRepository campaigns)
{
    public async Task<IReadOnlyList<CampaignInvitationDto>> HandleAsync(Guid currentUserId, Guid campaignId, CancellationToken cancellationToken = default)
    {
        await access.RequireAsync(campaignId, currentUserId, CampaignRole.DM, cancellationToken);
        return await campaigns.ListInvitationsForCampaignAsync(campaignId, cancellationToken);
    }
}

/// <summary>Pending invitations of the current user, newest first.</summary>
public sealed class ListMyInvitationsHandler(ICampaignRepository campaigns)
{
    public Task<IReadOnlyList<MyInvitationDto>> HandleAsync(Guid currentUserId, CancellationToken cancellationToken = default) =>
        campaigns.ListInvitationsForUserAsync(currentUserId, cancellationToken);
}

/// <summary>The invited user accepts: they join the campaign with the invited role.</summary>
public sealed class AcceptInvitationHandler(
    ICampaignRepository campaigns,
    IUserRepository users,
    IUnitOfWork unitOfWork,
    ICampaignNotifier notifier,
    IDateTimeProvider clock)
{
    public async Task<MemberDto> HandleAsync(Guid currentUserId, Guid invitationId, CancellationToken cancellationToken = default)
    {
        var invitation = await campaigns.GetInvitationAsync(invitationId, cancellationToken);
        if (invitation is null || invitation.UserId != currentUserId)
        {
            throw InvitationErrors.NotFound();
        }

        var user = await users.GetByIdAsync(currentUserId, cancellationToken) ?? throw InvitationErrors.NotFound();
        var campaign = await campaigns.GetWithMembersAsync(invitation.CampaignId, cancellationToken) ?? throw CampaignErrors.CampaignNotFound();
        var member = campaign.AcceptInvitation(invitation, clock.UtcNow);
        campaigns.RemoveInvitation(invitation);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await notifier.MembersUpdatedAsync(campaign.Id, clock.UtcNow, cancellationToken);

        return new MemberDto(user.Id, user.DisplayName, user.Email, member.Role.ToString(), member.JoinedAt);
    }
}

/// <summary>The invited user declines the invitation.</summary>
public sealed class DeclineInvitationHandler(
    ICampaignRepository campaigns,
    IUnitOfWork unitOfWork,
    ICampaignNotifier notifier,
    IDateTimeProvider clock)
{
    public async Task HandleAsync(Guid currentUserId, Guid invitationId, CancellationToken cancellationToken = default)
    {
        var invitation = await campaigns.GetInvitationAsync(invitationId, cancellationToken);
        if (invitation is null || invitation.UserId != currentUserId)
        {
            throw InvitationErrors.NotFound();
        }

        campaigns.RemoveInvitation(invitation);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await notifier.MembersUpdatedAsync(invitation.CampaignId, clock.UtcNow, cancellationToken);
    }
}

/// <summary>A DM cancels a pending invitation of their campaign.</summary>
public sealed class CancelInvitationHandler(
    ICampaignAccess access,
    ICampaignRepository campaigns,
    IUnitOfWork unitOfWork,
    ICampaignNotifier notifier,
    IDateTimeProvider clock)
{
    public async Task HandleAsync(Guid currentUserId, Guid campaignId, Guid invitationId, CancellationToken cancellationToken = default)
    {
        await access.RequireAsync(campaignId, currentUserId, CampaignRole.DM, cancellationToken);
        var invitation = await campaigns.GetInvitationAsync(invitationId, cancellationToken);
        if (invitation is null || invitation.CampaignId != campaignId)
        {
            throw InvitationErrors.NotFound();
        }

        campaigns.RemoveInvitation(invitation);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await notifier.MembersUpdatedAsync(campaignId, clock.UtcNow, cancellationToken);
    }
}
