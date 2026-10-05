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

/// <summary>Adds an active user to the campaign. At least DM adds players; only the owner adds DMs.</summary>
public sealed class AddMemberHandler(
    ICampaignAccess access,
    ICampaignRepository campaigns,
    IUserRepository users,
    IUnitOfWork unitOfWork,
    IDateTimeProvider clock)
{
    public async Task<MemberDto> HandleAsync(Guid currentUserId, Guid campaignId, AddMemberRequest request, CancellationToken cancellationToken = default)
    {
        await access.RequireAsync(campaignId, currentUserId, CampaignRole.DM, cancellationToken);

        var user = await users.GetByIdAsync(request.UserId, cancellationToken);
        if (user is null || !user.IsActive)
        {
            throw AppException.NotFound("El usuario no existe o está desactivado.");
        }

        var campaign = await campaigns.GetWithMembersAsync(campaignId, cancellationToken) ?? throw CampaignErrors.CampaignNotFound();
        var member = campaign.AddMember(currentUserId, user.Id, CampaignRoles.ParseAssignable(request.Role), clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new MemberDto(user.Id, user.DisplayName, user.Email, member.Role.ToString(), member.JoinedAt);
    }
}
