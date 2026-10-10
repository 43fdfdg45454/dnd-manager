using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using OpenTrpg.Core.Application.Abstractions;
using OpenTrpg.Core.Application.Characters;
using OpenTrpg.Core.Application.Items;
using OpenTrpg.Core.Application.Party;
using OpenTrpg.Core.Application;
using OpenTrpg.Core.Application.Systems;
using OpenTrpg.Systems.Dnd5e.Application;
using OpenTrpg.Systems.Dnd5e.Application.Abstractions;
using OpenTrpg.Systems.Dnd5e.Application.Catalog;
using OpenTrpg.Systems.Dnd5e.Application.Characters;
using OpenTrpg.Systems.Dnd5e.Application.Items;
using OpenTrpg.Systems.Dnd5e.Application.Party;

namespace OpenTrpg.Systems.Dnd5e.Application;

/// <summary>The application services of the D&amp;D 5e module: its handlers, planners and contract parts.</summary>
public static class Dnd5eApplicationServices
{
    public static IServiceCollection AddDnd5eApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(Dnd5eApplicationServices).Assembly);

        services.AddScoped<GetAttributionHandler>();
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
        services.AddScoped<IEquippedGearProvider, InventoryEquippedGearProvider>();
        services.AddScoped<ICharacterSheetService, CharacterSheetService>();
        services.AddScoped<Dnd5eCharacterLoader>();
        services.AddScoped<Dnd5eCharacterParts>();
        services.AddScoped<Dnd5eSheetSystem>();
        services.AddScoped<Dnd5eCreationSystem>();
        services.AddScoped<Dnd5eProgressionSystem>();
        services.AddScoped<Dnd5eCombatSystem>();
        services.AddScoped<Dnd5eRestSystem>();
        services.AddScoped<Dnd5eChoiceSystem>();
        services.AddScoped<Dnd5ePartySystem>();
        services.AddScoped<Dnd5eItemSystem>();
        services.AddScoped<Dnd5eChangeRequestSystem>();
        services.AddScoped<CharacterTracker>();
        services.AddScoped<CompanionPlanner>();
        services.AddScoped<SetCompanionHandler>();
        services.AddScoped<CompanionTrackingHandler>();
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
        services.AddScoped<Dnd5ePartyLoader>();
        services.AddScoped<GetPartyHandler>();
        services.AddScoped<PartyRestHandler>();
        services.AddScoped<PartyAdjustHandler>();
        services.AddScoped<PartyLevelHandler>();

        return services;
    }
}
