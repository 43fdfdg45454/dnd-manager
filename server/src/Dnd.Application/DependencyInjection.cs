using System.Globalization;
using Dnd.Application.Auth;
using Dnd.Application.Campaigns;
using Dnd.Application.Users;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Dnd.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Default FluentValidation messages (rules without a custom message) in Spanish.
        ValidatorOptions.Global.LanguageManager.Culture = new CultureInfo("es");
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);

        services.AddScoped<AuthSessionIssuer>();
        services.AddScoped<PasswordTokenIssuer>();

        services.AddScoped<LoginHandler>();
        services.AddScoped<RefreshHandler>();
        services.AddScoped<LogoutHandler>();
        services.AddScoped<ForgotPasswordHandler>();
        services.AddScoped<SetPasswordHandler>();
        services.AddScoped<GetMeHandler>();

        services.AddScoped<ListUsersHandler>();
        services.AddScoped<CreateUserHandler>();
        services.AddScoped<UpdateUserHandler>();
        services.AddScoped<ResendSetupEmailHandler>();
        services.AddScoped<InitialAdminSeeder>();
        services.AddScoped<SearchUsersHandler>();

        services.AddScoped<ListMyCampaignsHandler>();
        services.AddScoped<CreateCampaignHandler>();
        services.AddScoped<GetCampaignHandler>();
        services.AddScoped<UpdateCampaignHandler>();
        services.AddScoped<DeleteCampaignHandler>();
        services.AddScoped<ListMembersHandler>();
        services.AddScoped<AddMemberHandler>();
        services.AddScoped<ChangeMemberRoleHandler>();
        services.AddScoped<RemoveMemberHandler>();
        services.AddScoped<LeaveCampaignHandler>();
        services.AddScoped<TransferOwnershipHandler>();

        return services;
    }
}
