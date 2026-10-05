using System.Globalization;
using Dnd.Application.Abstractions;
using Dnd.Application.Auth;
using Dnd.Application.Campaigns;
using Dnd.Application.Catalog;
using Dnd.Application.ChangeRequests;
using Dnd.Application.Characters;
using Dnd.Application.Items;
using Dnd.Application.Users;
using Dnd.Domain.Characters;
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

        services.AddSingleton<GetAttributionHandler>();
        services.AddScoped<ListClassesHandler>();
        services.AddScoped<GetClassHandler>();
        services.AddScoped<GetFeatureHandler>();
        services.AddScoped<ListRacesHandler>();
        services.AddScoped<GetRaceHandler>();
        services.AddScoped<SearchSpellsHandler>();
        services.AddScoped<GetSpellHandler>();
        services.AddScoped<SearchItemsHandler>();
        services.AddScoped<GetItemHandler>();
        services.AddScoped<ListConditionsHandler>();
        services.AddScoped<ListSkillsHandler>();
        services.AddScoped<ListBackgroundsHandler>();

        services.AddScoped<IEquippedGearProvider, InventoryEquippedGearProvider>();
        services.AddSingleton<IDiceRoller>(RandomDiceRoller.Instance);
        services.AddScoped<ICharacterSheetService, CharacterSheetService>();
        services.AddScoped<CharacterLoader>();
        services.AddScoped<CharacterTracker>();
        services.AddScoped<ListCharactersHandler>();
        services.AddScoped<CreateCharacterHandler>();
        services.AddScoped<GetCharacterHandler>();
        services.AddScoped<UpdateSheetHandler>();
        services.AddScoped<SubmitCharacterHandler>();
        services.AddScoped<ActivateCharacterHandler>();
        services.AddScoped<DeleteCharacterHandler>();
        services.AddScoped<UpdateCombatHandler>();
        services.AddScoped<SetConcentrationHandler>();
        services.AddScoped<SpellSlotHandler>();
        services.AddScoped<ResourceHandler>();
        services.AddScoped<RestHandler>();

        services.AddScoped<ChangeRequestLoader>();
        services.AddScoped<ListChangeRequestsHandler>();
        services.AddScoped<GetChangeRequestHandler>();
        services.AddScoped<ApproveChangeRequestHandler>();
        services.AddScoped<RejectChangeRequestHandler>();
        services.AddScoped<CancelChangeRequestHandler>();

        services.AddScoped<SearchCampaignItemsHandler>();
        services.AddScoped<GetCampaignItemHandler>();
        services.AddScoped<CreateHomebrewItemHandler>();
        services.AddScoped<UpdateHomebrewItemHandler>();
        services.AddScoped<DeleteHomebrewItemHandler>();
        services.AddScoped<InventoryReader>();
        services.AddScoped<InventoryOperations>();
        services.AddScoped<GetInventoryHandler>();
        services.AddScoped<AddInventoryItemHandler>();
        services.AddScoped<UpdateInventoryItemHandler>();
        services.AddScoped<UseInventoryItemHandler>();
        services.AddScoped<RemoveInventoryItemHandler>();
        services.AddScoped<AdjustMoneyHandler>();
        services.AddScoped<ShopLoader>();
        services.AddScoped<TradeLoader>();
        services.AddScoped<ListShopsHandler>();
        services.AddScoped<CreateShopHandler>();
        services.AddScoped<GetShopHandler>();
        services.AddScoped<UpdateShopHandler>();
        services.AddScoped<DeleteShopHandler>();
        services.AddScoped<AddShopItemHandler>();
        services.AddScoped<UpdateShopItemHandler>();
        services.AddScoped<DeleteShopItemHandler>();
        services.AddScoped<BuyHandler>();
        services.AddScoped<SellHandler>();
        services.AddScoped<ListTransactionsHandler>();

        return services;
    }
}
