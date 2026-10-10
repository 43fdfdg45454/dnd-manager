using OpenTrpg.Core.Application.Abstractions;
using OpenTrpg.Core.Application.Abstractions.Persistence;
using OpenTrpg.Core.Application.Characters;
using OpenTrpg.Core.Domain.Campaigns;
using FluentValidation;

namespace OpenTrpg.Core.Application.Campaigns;

/// <param name="PreviousOwnerRole">Role the current owner keeps: "DM" or "Player".</param>
public sealed record TransferOwnershipRequest(Guid ToUserId, string PreviousOwnerRole);

public sealed class TransferOwnershipRequestValidator : AbstractValidator<TransferOwnershipRequest>
{
    public TransferOwnershipRequestValidator()
    {
        RuleFor(x => x.ToUserId).NotEmpty().WithMessage("Indica el nuevo propietario.");
        RuleFor(x => x.PreviousOwnerRole).Must(CampaignRoles.IsAssignable).WithMessage(CampaignRoles.AssignableMessage);
    }
}

/// <summary>
/// Hands the campaign over to another member and records an <see cref="OwnershipTransfer"/>. Owner only.
/// A member who still owns characters in the campaign cannot become owner (409).
/// </summary>
public sealed class TransferOwnershipHandler(
    ICampaignAccess access,
    ICampaignRepository campaigns,
    IUserRepository users,
    CharacterOwnerRules ownerRules,
    IUnitOfWork unitOfWork,
    IDateTimeProvider clock)
{
    public async Task<CampaignDto> HandleAsync(Guid currentUserId, Guid campaignId, TransferOwnershipRequest request, CancellationToken cancellationToken = default)
    {
        await access.RequireAsync(campaignId, currentUserId, CampaignRole.Owner, cancellationToken);

        var campaign = await campaigns.GetWithMembersAsync(campaignId, cancellationToken) ?? throw CampaignErrors.CampaignNotFound();
        if (request.ToUserId != currentUserId
            && campaign.FindMember(request.ToUserId) is not null
            && await users.GetByIdAsync(request.ToUserId, cancellationToken) is { } target)
        {
            await ownerRules.EnsureOwnsNoCharactersAsync(campaignId, target.Id, target.DisplayName, cancellationToken);
        }

        var transfer = campaign.TransferOwnership(
            currentUserId,
            request.ToUserId,
            CampaignRoles.ParseAssignable(request.PreviousOwnerRole),
            clock.UtcNow);
        campaigns.AddOwnershipTransfer(transfer);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var members = await campaigns.ListMembersAsync(campaignId, cancellationToken);
        return CampaignDto.From(campaign, members, currentUserId);
    }
}
