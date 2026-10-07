using Dnd.Application.Abstractions;
using Dnd.Application.Abstractions.Persistence;
using Dnd.Application.Characters;
using Dnd.Application.Common;
using Dnd.Domain.Campaigns;
using FluentValidation;

namespace Dnd.Application.Campaigns;

/// <param name="Role">"DM" or "Player".</param>
public sealed record ChangeMemberRoleRequest(string Role);

public sealed class ChangeMemberRoleRequestValidator : AbstractValidator<ChangeMemberRoleRequest>
{
    public ChangeMemberRoleRequestValidator()
    {
        RuleFor(x => x.Role).Must(CampaignRoles.IsAssignable).WithMessage(CampaignRoles.AssignableMessage);
    }
}

/// <summary>
/// Switches a member between DM and Player. Owner only; the owner's own role cannot change here. A
/// player who still owns characters in the campaign cannot become DM (409).
/// </summary>
public sealed class ChangeMemberRoleHandler(
    ICampaignAccess access,
    ICampaignRepository campaigns,
    IUserRepository users,
    CharacterOwnerRules ownerRules,
    IUnitOfWork unitOfWork)
{
    public async Task<MemberDto> HandleAsync(Guid currentUserId, Guid campaignId, Guid userId, ChangeMemberRoleRequest request, CancellationToken cancellationToken = default)
    {
        await access.RequireAsync(campaignId, currentUserId, CampaignRole.Owner, cancellationToken);

        var campaign = await campaigns.GetWithMembersAsync(campaignId, cancellationToken) ?? throw CampaignErrors.CampaignNotFound();
        var role = CampaignRoles.ParseAssignable(request.Role);
        if (role == CampaignRole.DM
            && campaign.RoleOf(userId) == CampaignRole.Player
            && await users.GetByIdAsync(userId, cancellationToken) is { } promoted)
        {
            await ownerRules.EnsureOwnsNoCharactersAsync(campaignId, userId, promoted.DisplayName, cancellationToken);
        }

        var member = campaign.ChangeRole(currentUserId, userId, role);
        var user = await users.GetByIdAsync(userId, cancellationToken)
            ?? throw AppException.NotFound("Usuario no encontrado.");
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new MemberDto(user.Id, user.DisplayName, user.Email, member.Role.ToString(), member.JoinedAt);
    }
}
