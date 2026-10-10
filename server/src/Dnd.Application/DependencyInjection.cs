using System.Globalization;
using Dnd.Application.Abstractions;
using Dnd.Application.Auth;
using Dnd.Application.Campaigns;
using Dnd.Application.Catalog;
using Dnd.Application.ChangeRequests;
using Dnd.Application.ContentPacks;
using Dnd.Application.Characters;
using Dnd.Application.Files;
using Dnd.Application.Library;
using Dnd.Application.Lore;
using Dnd.Application.Maps;
using Dnd.Application.Messages;
using Dnd.Application.Party;
using Dnd.Application.Releases;
using Dnd.Application.Sessions;
using Dnd.Application.Setup;
using Dnd.Application.Systems;
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
        services.AddScoped<UpdateProfileHandler>();

        services.AddScoped<ListUsersHandler>();
        services.AddScoped<CreateUserHandler>();
        services.AddScoped<UpdateUserHandler>();
        services.AddScoped<ResendSetupEmailHandler>();
        services.AddScoped<SearchUsersHandler>();

        services.AddScoped<ListMyCampaignsHandler>();
        services.AddScoped<CreateCampaignHandler>();
        services.AddScoped<GetCampaignHandler>();
        services.AddScoped<UpdateCampaignHandler>();
        services.AddScoped<DeleteCampaignHandler>();
        services.AddScoped<ListMembersHandler>();
        services.AddScoped<InviteMemberHandler>();
        services.AddScoped<ListCampaignInvitationsHandler>();
        services.AddScoped<ListMyInvitationsHandler>();
        services.AddScoped<AcceptInvitationHandler>();
        services.AddScoped<DeclineInvitationHandler>();
        services.AddScoped<CancelInvitationHandler>();
        services.AddScoped<ChangeMemberRoleHandler>();
        services.AddScoped<RemoveMemberHandler>();
        services.AddScoped<LeaveCampaignHandler>();
        services.AddScoped<TransferOwnershipHandler>();

        services.AddSingleton<ListSystemsHandler>();
        services.AddSingleton<GetAttributionHandler>();
        services.AddScoped<ListClassesHandler>();
        services.AddScoped<GetClassHandler>();
        services.AddScoped<GetFeatureHandler>();
        services.AddScoped<ListRacesHandler>();
        services.AddScoped<GetRaceHandler>();
        services.AddScoped<SearchSpellsHandler>();
        services.AddScoped<GetSpellHandler>();
        services.AddScoped<SearchBeastsHandler>();
        services.AddScoped<GetBeastHandler>();
        services.AddScoped<SearchItemsHandler>();
        services.AddScoped<GetItemHandler>();
        services.AddScoped<ListConditionsHandler>();
        services.AddScoped<ListSkillsHandler>();
        services.AddScoped<ListBackgroundsHandler>();
        services.AddScoped<GetEquipmentCategoryHandler>();
        services.AddScoped<ListCatalogSourcesHandler>();
        services.AddScoped<ListTrinketsHandler>();
        services.AddScoped<ListRollTablesHandler>();

        services.AddScoped<ListContentPacksHandler>();
        services.AddScoped<ImportContentPackHandler>();
        services.AddScoped<DeleteContentPackHandler>();

        // Realtime events: nothing by default; the API host replaces it with the SignalR notifier.
        services.AddSingleton<ICampaignNotifier, NoopCampaignNotifier>();
        services.AddSingleton<IRealtimeConnections, NoopRealtimeConnections>();

        services.AddScoped<IEquippedGearProvider, InventoryEquippedGearProvider>();
        services.AddSingleton<IDiceRoller>(RandomDiceRoller.Instance);
        services.AddScoped<ICharacterSheetService, CharacterSheetService>();
        services.AddScoped<CharacterLoader>();
        services.AddScoped<CharacterTracker>();
        services.AddScoped<ListCharactersHandler>();
        services.AddScoped<CharacterOwnerRules>();
        services.AddScoped<CreateCharacterHandler>();
        services.AddScoped<SetCharacterOwnerHandler>();
        services.AddScoped<GetCharacterHandler>();
        services.AddScoped<UpdateSheetHandler>();
        services.AddScoped<CompanionPlanner>();
        services.AddScoped<SetCompanionHandler>();
        services.AddScoped<CompanionTrackingHandler>();
        services.AddScoped<SubmitCharacterHandler>();
        services.AddScoped<ActivateCharacterHandler>();
        services.AddScoped<DeleteCharacterHandler>();
        services.AddScoped<UpdateCombatHandler>();
        services.AddScoped<SetConcentrationHandler>();
        services.AddScoped<ApplyDamageHandler>();
        services.AddScoped<OriginChoicesPlanner>();
        services.AddScoped<OriginChoicesHandler>();
        services.AddScoped<InvalidChoicesPlanner>();
        services.AddScoped<InvalidChoicesHandler>();
        services.AddScoped<SpellSlotHandler>();
        services.AddScoped<ResourceHandler>();
        services.AddScoped<RestHandler>();
        services.AddScoped<ClassActionHandler>();
        services.AddScoped<LevelUpPlanner>();
        services.AddScoped<GetLevelUpPlanHandler>();
        services.AddScoped<ApplyLevelUpHandler>();
        services.AddScoped<SpellPreparationPlanner>();
        services.AddScoped<SpellPreparationHandler>();

        services.AddScoped<ChangeRequestLoader>();
        services.AddScoped<ListChangeRequestsHandler>();
        services.AddScoped<GetChangeRequestHandler>();
        services.AddScoped<ApproveChangeRequestHandler>();
        services.AddScoped<RejectChangeRequestHandler>();
        services.AddScoped<CancelChangeRequestHandler>();

        services.AddScoped<RestRequestLoader>();
        services.AddScoped<CreateRestRequestHandler>();
        services.AddScoped<CancelRestRequestHandler>();
        services.AddScoped<ListRestRequestsHandler>();
        services.AddScoped<GetRestRequestHandler>();
        services.AddScoped<ApproveRestRequestHandler>();
        services.AddScoped<RejectRestRequestHandler>();

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
        services.AddScoped<AddShopItemsBulkHandler>();
        services.AddScoped<UpdateShopItemHandler>();
        services.AddScoped<DeleteShopItemHandler>();
        services.AddScoped<BuyHandler>();
        services.AddScoped<SellHandler>();
        services.AddScoped<ListTransactionsHandler>();
        services.AddScoped<StashSupport>();
        services.AddScoped<GetStashHandler>();
        services.AddScoped<AddStashItemHandler>();
        services.AddScoped<UpdateStashItemHandler>();
        services.AddScoped<DeleteStashItemHandler>();
        services.AddScoped<TakeStashItemHandler>();
        services.AddScoped<ReturnStashItemHandler>();
        services.AddScoped<StashGoldHandler>();
        services.AddScoped<SplitStashGoldHandler>();

        services.AddScoped<PartyLoader>();
        services.AddScoped<GetPartyHandler>();
        services.AddScoped<PartyRestHandler>();
        services.AddScoped<PartyAdjustHandler>();
        services.AddScoped<PartyLevelHandler>();

        services.AddScoped<SendMessageHandler>();
        services.AddScoped<ListMessagesHandler>();
        services.AddScoped<GetUnreadCountHandler>();
        services.AddScoped<MarkMessageReadHandler>();

        services.AddScoped<FileCleanup>();
        services.AddScoped<CampaignFileGuard>();
        services.AddScoped<UploadFileHandler>();
        services.AddScoped<GetFileHandler>();
        services.AddScoped<SetPortraitHandler>();

        services.AddScoped<LoreLoader>();
        services.AddScoped<ListLoreHandler>();
        services.AddScoped<CreateLoreHandler>();
        services.AddScoped<GetLoreHandler>();
        services.AddScoped<UpdateLoreHandler>();
        services.AddScoped<DeleteLoreHandler>();
        services.AddScoped<AddLoreAttachmentHandler>();
        services.AddScoped<DeleteLoreAttachmentHandler>();

        services.AddScoped<MapLoader>();
        services.AddScoped<ListMapsHandler>();
        services.AddScoped<CreateMapHandler>();
        services.AddScoped<GetMapHandler>();
        services.AddScoped<UpdateMapHandler>();
        services.AddScoped<DeleteMapHandler>();
        services.AddScoped<CreatePinHandler>();
        services.AddScoped<UpdatePinHandler>();
        services.AddScoped<DeletePinHandler>();

        services.AddScoped<LibraryDocumentMapper>();
        services.AddScoped<ListLibraryHandler>();
        services.AddScoped<CreateLibraryDocumentHandler>();
        services.AddScoped<UpdateLibraryDocumentHandler>();
        services.AddScoped<DeleteLibraryDocumentHandler>();
        services.AddScoped<ListCampaignLibraryHandler>();
        services.AddScoped<RecommendDocumentHandler>();
        services.AddScoped<RemoveRecommendationHandler>();

        services.AddScoped<SessionLoader>();
        services.AddScoped<SessionViewBuilder>();
        services.AddScoped<PublicSessionLoader>();
        services.AddScoped<ListSessionsHandler>();
        services.AddScoped<CreateSessionHandler>();
        services.AddScoped<GetSessionHandler>();
        services.AddScoped<UpdateSessionHandler>();
        services.AddScoped<DeleteSessionHandler>();
        services.AddScoped<RespondToSessionHandler>();
        services.AddScoped<SetSessionSummaryHandler>();
        services.AddScoped<GetJournalHandler>();
        services.AddScoped<ListMySessionsHandler>();
        services.AddScoped<NotifySessionHandler>();
        services.AddScoped<UpdateCampaignSettingsHandler>();
        services.AddScoped<GetPublicSessionHandler>();
        services.AddScoped<PublicRsvpHandler>();
        services.AddScoped<ReminderProcessor>();

        services.AddScoped<GetLatestReleaseHandler>();
        services.AddScoped<DownloadReleaseHandler>();
        services.AddScoped<ListReleasesHandler>();
        services.AddScoped<PublishReleaseHandler>();
        services.AddScoped<DeleteReleaseHandler>();
        services.AddScoped<GetAdminStatsHandler>();

        services.AddScoped<GetSetupStatusHandler>();
        services.AddScoped<CreateInitialAdminHandler>();

        return services;
    }
}
