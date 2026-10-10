using OpenTrpg.Core.Application.Abstractions;
using OpenTrpg.Core.Application.Abstractions.Persistence;
using OpenTrpg.Core.Domain.Campaigns;
using FluentValidation;

namespace OpenTrpg.Core.Application.Campaigns;

/// <summary>Partial update: null fields are left unchanged.</summary>
public sealed record UpdateCampaignRequest(string? Name, string? Description);

public sealed class UpdateCampaignRequestValidator : AbstractValidator<UpdateCampaignRequest>
{
    public UpdateCampaignRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("El nombre no puede estar vacío.")
            .MaximumLength(Campaign.NameMaxLength)
            .WithMessage($"El nombre no puede superar los {Campaign.NameMaxLength} caracteres.")
            .When(x => x.Name is not null);
        RuleFor(x => x.Description)
            .MaximumLength(Campaign.DescriptionMaxLength)
            .WithMessage($"La descripción no puede superar los {Campaign.DescriptionMaxLength} caracteres.");
    }
}

/// <summary>Edits name and description. Requires at least DM.</summary>
public sealed class UpdateCampaignHandler(
    ICampaignAccess access,
    ICampaignRepository campaigns,
    IUnitOfWork unitOfWork,
    IDateTimeProvider clock)
{
    public async Task<CampaignDto> HandleAsync(Guid currentUserId, Guid campaignId, UpdateCampaignRequest request, CancellationToken cancellationToken = default)
    {
        await access.RequireAsync(campaignId, currentUserId, CampaignRole.DM, cancellationToken);

        var campaign = await campaigns.GetWithMembersAsync(campaignId, cancellationToken) ?? throw CampaignErrors.CampaignNotFound();
        campaign.UpdateDetails(currentUserId, request.Name, request.Description, clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var members = await campaigns.ListMembersAsync(campaignId, cancellationToken);
        return CampaignDto.From(campaign, members, currentUserId);
    }
}
