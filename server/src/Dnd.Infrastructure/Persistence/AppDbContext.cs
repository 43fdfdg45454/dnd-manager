using Dnd.Application.Abstractions.Persistence;
using Dnd.Domain.Campaigns;
using Dnd.Domain.Catalog;
using Dnd.Domain.Characters;
using Dnd.Domain.Common;
using Dnd.Domain.Files;
using Dnd.Domain.Items;
using Dnd.Domain.Library;
using Dnd.Domain.Lore;
using Dnd.Domain.Maps;
using Dnd.Domain.Messages;
using Dnd.Domain.Releases;
using Dnd.Domain.Sessions;
using Dnd.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace Dnd.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), IUnitOfWork
{
    public DbSet<User> Users => Set<User>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<PasswordToken> PasswordTokens => Set<PasswordToken>();

    public DbSet<Campaign> Campaigns => Set<Campaign>();

    public DbSet<CampaignMember> CampaignMembers => Set<CampaignMember>();

    public DbSet<OwnershipTransfer> OwnershipTransfers => Set<OwnershipTransfer>();

    public DbSet<CampaignInvitation> CampaignInvitations => Set<CampaignInvitation>();

    public DbSet<ClassDefinition> CatalogClasses => Set<ClassDefinition>();

    public DbSet<ClassLevel> CatalogClassLevels => Set<ClassLevel>();

    public DbSet<SubclassDefinition> CatalogSubclasses => Set<SubclassDefinition>();

    public DbSet<SubclassLevel> CatalogSubclassLevels => Set<SubclassLevel>();

    public DbSet<FeatureDefinition> CatalogFeatures => Set<FeatureDefinition>();

    public DbSet<RaceDefinition> CatalogRaces => Set<RaceDefinition>();

    public DbSet<SubraceDefinition> CatalogSubraces => Set<SubraceDefinition>();

    public DbSet<RaceExtensionDefinition> CatalogRaceExtensions => Set<RaceExtensionDefinition>();

    public DbSet<TraitDefinition> CatalogTraits => Set<TraitDefinition>();

    public DbSet<SpellDefinition> CatalogSpells => Set<SpellDefinition>();

    public DbSet<ItemTemplate> ItemTemplates => Set<ItemTemplate>();

    public DbSet<ConditionDefinition> CatalogConditions => Set<ConditionDefinition>();

    public DbSet<SkillDefinition> CatalogSkills => Set<SkillDefinition>();

    public DbSet<BackgroundDefinition> CatalogBackgrounds => Set<BackgroundDefinition>();

    public DbSet<CatalogImport> CatalogImports => Set<CatalogImport>();

    public DbSet<EquipmentCategory> CatalogEquipmentCategories => Set<EquipmentCategory>();

    public DbSet<OptionSetDefinition> CatalogOptionSets => Set<OptionSetDefinition>();

    public DbSet<OptionDefinition> CatalogOptions => Set<OptionDefinition>();

    public DbSet<LevelChoiceRule> CatalogLevelChoiceRules => Set<LevelChoiceRule>();

    public DbSet<TrinketEntry> CatalogTrinkets => Set<TrinketEntry>();

    public DbSet<RollTable> CatalogRollTables => Set<RollTable>();

    public DbSet<Character> Characters => Set<Character>();

    public DbSet<CharacterClassLevel> CharacterClassLevels => Set<CharacterClassLevel>();

    public DbSet<CharacterProficiency> CharacterProficiencies => Set<CharacterProficiency>();

    public DbSet<CharacterSpell> CharacterSpells => Set<CharacterSpell>();

    public DbSet<SpellSlotState> CharacterSpellSlots => Set<SpellSlotState>();

    public DbSet<CharacterResource> CharacterResources => Set<CharacterResource>();

    public DbSet<CharacterOverride> CharacterOverrides => Set<CharacterOverride>();

    public DbSet<CharacterChoice> CharacterChoices => Set<CharacterChoice>();

    public DbSet<ChangeRequest> ChangeRequests => Set<ChangeRequest>();

    public DbSet<RestRequest> RestRequests => Set<RestRequest>();

    public DbSet<CharacterCompanion> CharacterCompanions => Set<CharacterCompanion>();

    public DbSet<CharacterItem> CharacterItems => Set<CharacterItem>();

    public DbSet<Shop> Shops => Set<Shop>();

    public DbSet<ShopItem> ShopItems => Set<ShopItem>();

    public DbSet<Transaction> Transactions => Set<Transaction>();

    public DbSet<PartyStashItem> PartyStashItems => Set<PartyStashItem>();

    public DbSet<DirectMessage> DirectMessages => Set<DirectMessage>();

    public DbSet<StoredFile> StoredFiles => Set<StoredFile>();

    public DbSet<LoreEntry> LoreEntries => Set<LoreEntry>();

    public DbSet<LoreAttachment> LoreAttachments => Set<LoreAttachment>();

    public DbSet<Map> Maps => Set<Map>();

    public DbSet<MapPin> MapPins => Set<MapPin>();

    public DbSet<LibraryDocument> LibraryDocuments => Set<LibraryDocument>();

    public DbSet<CampaignDocument> CampaignDocuments => Set<CampaignDocument>();

    public DbSet<AppRelease> AppReleases => Set<AppRelease>();

    public DbSet<GameSession> GameSessions => Set<GameSession>();

    public DbSet<SessionRsvp> SessionRsvps => Set<SessionRsvp>();

    public DbSet<Reminder> Reminders => Set<Reminder>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
