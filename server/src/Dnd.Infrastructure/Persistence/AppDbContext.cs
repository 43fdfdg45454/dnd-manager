using Dnd.Application.Abstractions.Persistence;
using Dnd.Domain.Campaigns;
using Dnd.Domain.Catalog;
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

    public DbSet<ClassDefinition> CatalogClasses => Set<ClassDefinition>();

    public DbSet<ClassLevel> CatalogClassLevels => Set<ClassLevel>();

    public DbSet<SubclassDefinition> CatalogSubclasses => Set<SubclassDefinition>();

    public DbSet<SubclassLevel> CatalogSubclassLevels => Set<SubclassLevel>();

    public DbSet<FeatureDefinition> CatalogFeatures => Set<FeatureDefinition>();

    public DbSet<RaceDefinition> CatalogRaces => Set<RaceDefinition>();

    public DbSet<SubraceDefinition> CatalogSubraces => Set<SubraceDefinition>();

    public DbSet<TraitDefinition> CatalogTraits => Set<TraitDefinition>();

    public DbSet<SpellDefinition> CatalogSpells => Set<SpellDefinition>();

    public DbSet<ItemTemplate> ItemTemplates => Set<ItemTemplate>();

    public DbSet<ConditionDefinition> CatalogConditions => Set<ConditionDefinition>();

    public DbSet<SkillDefinition> CatalogSkills => Set<SkillDefinition>();

    public DbSet<BackgroundDefinition> CatalogBackgrounds => Set<BackgroundDefinition>();

    public DbSet<CatalogImport> CatalogImports => Set<CatalogImport>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
