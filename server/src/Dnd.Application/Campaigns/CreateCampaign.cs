using Dnd.Application.Abstractions;
using Dnd.Application.Abstractions.Persistence;
using Dnd.Application.Common;
using Dnd.Domain.Campaigns;
using FluentValidation;

namespace Dnd.Application.Campaigns;

public sealed record CreateCampaignRequest(string Name, string? Description);

public sealed class CreateCampaignRequestValidator : AbstractValidator<CreateCampaignRequest>
{
    public CreateCampaignRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Introduce el nombre de la campaña.")
            .MaximumLength(Campaign.NameMaxLength)
            .WithMessage($"El nombre no puede superar los {Campaign.NameMaxLength} caracteres.");
        RuleFor(x => x.Description)
            .MaximumLength(Campaign.DescriptionMaxLength)
            .WithMessage($"La descripción no puede superar los {Campaign.DescriptionMaxLength} caracteres.");
    }
}

/// <summary>Creates a campaign owned by the current user, who becomes its first member.</summary>
public sealed class CreateCampaignHandler(
    ICampaignRepository campaigns,
    IUserRepository users,
    IUnitOfWork unitOfWork,
    IDateTimeProvider clock)
{
    public async Task<CampaignDto> HandleAsync(Guid currentUserId, CreateCampaignRequest request, CancellationToken cancellationToken = default)
    {
        var user = await users.GetByIdAsync(currentUserId, cancellationToken)
            ?? throw AppException.Unauthorized("La sesión no es válida.");
        if (!user.IsActive)
        {
            throw AppException.Forbidden("Tu cuenta está desactivada.");
        }

        var campaign = Campaign.Create(request.Name, request.Description, user.Id, clock.UtcNow);
        campaigns.Add(campaign);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var owner = campaign.FindMember(user.Id)!;
        MemberDto[] members = [new(user.Id, user.DisplayName, user.Email, owner.Role.ToString(), owner.JoinedAt)];
        return CampaignDto.From(campaign, members, user.Id);
    }
}
