using OpenTrpg.Core.Application.Abstractions;
using OpenTrpg.Core.Application.Abstractions.Persistence;
using OpenTrpg.Core.Application.Common;
using OpenTrpg.Core.Application.Systems;
using OpenTrpg.Core.Domain.Campaigns;
using FluentValidation;

namespace OpenTrpg.Core.Application.Campaigns;

/// <param name="SystemId">Game system of the campaign; null uses the instance's default system.</param>
public sealed record CreateCampaignRequest(string Name, string? Description, string? SystemId = null);

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
        RuleFor(x => x.SystemId)
            .MaximumLength(Campaign.SystemIdMaxLength)
            .WithMessage($"El sistema de juego no puede superar los {Campaign.SystemIdMaxLength} caracteres.");
    }
}

/// <summary>Creates a campaign owned by the current user, who becomes its first member.</summary>
public sealed class CreateCampaignHandler(
    ICampaignRepository campaigns,
    IUserRepository users,
    ICampaignDefaults defaults,
    IGameSystemRegistry systems,
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

        var system = request.SystemId is null
            ? systems.Default
            : systems.Find(request.SystemId) ?? throw UnknownSystem(request.SystemId);

        var campaign = Campaign.Create(request.Name, request.Description, user.Id, clock.UtcNow, defaults.TimeZoneId, system.Id);
        campaigns.Add(campaign);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var owner = campaign.FindMember(user.Id)!;
        MemberDto[] members = [new(user.Id, user.DisplayName, user.Email, owner.Role.ToString(), owner.JoinedAt)];
        return CampaignDto.From(campaign, members, user.Id);
    }

    private static AppException UnknownSystem(string systemId) =>
        AppException.Validation("systemId", $"Sistema de juego desconocido: «{systemId}».", "unknown-system");
}
