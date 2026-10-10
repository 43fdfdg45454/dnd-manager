using OpenTrpg.Core.Domain.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenTrpg.Core.Infrastructure;
using OpenTrpg.Core.Infrastructure.Persistence;
using OpenTrpg.Core.Infrastructure.Persistence.Configurations;
using OpenTrpg.Core.Infrastructure.Persistence.Configurations.Catalog;
using OpenTrpg.Systems.Dnd5e.Domain.Catalog;
using OpenTrpg.Systems.Dnd5e.Infrastructure.Persistence.Configurations.Catalog;

namespace OpenTrpg.Systems.Dnd5e.Infrastructure.Persistence.Configurations.Catalog;

internal sealed class ConditionDefinitionConfiguration : IEntityTypeConfiguration<ConditionDefinition>
{
    public void Configure(EntityTypeBuilder<ConditionDefinition> builder)
    {
        builder.ToTable("CatalogConditions");
        builder.HasKey(x => x.Index);
        builder.Property(x => x.Index).HasMaxLength(CatalogColumns.IndexMaxLength);
        builder.Property(x => x.Name).HasMaxLength(CatalogColumns.NameMaxLength).IsRequired();
        builder.HasIndex(x => x.Name);
        builder.Property(x => x.Description).HasJsonListConversion();
        builder.Property(x => x.Source).HasMaxLength(CatalogSources.MaxLength).IsRequired();
        builder.HasIndex(x => x.Source);
    }
}

internal sealed class CreatureDefinitionConfiguration : IEntityTypeConfiguration<CreatureDefinition>
{
    public void Configure(EntityTypeBuilder<CreatureDefinition> builder)
    {
        builder.ToTable("Dnd5eCreatures");
        builder.HasKey(x => x.Index);
        builder.Property(x => x.Index).HasMaxLength(CatalogColumns.IndexMaxLength);
        builder.Property(x => x.Name).HasMaxLength(CatalogColumns.NameMaxLength).IsRequired();
        builder.HasIndex(x => x.Name);
        builder.Property(x => x.Type).HasMaxLength(CreatureDefinition.TypeMaxLength).IsRequired();
        builder.HasIndex(x => x.Type);
        builder.Property(x => x.Subtype).HasMaxLength(CatalogColumns.NameMaxLength);
        builder.Property(x => x.Size).HasMaxLength(16).IsRequired();
        builder.Property(x => x.DataJson).IsRequired();
        builder.Property(x => x.Source).HasMaxLength(CatalogSources.MaxLength).IsRequired();
        builder.HasIndex(x => x.Source);
    }
}

internal sealed class RuleDefinitionConfiguration : IEntityTypeConfiguration<RuleDefinition>
{
    public void Configure(EntityTypeBuilder<RuleDefinition> builder)
    {
        builder.ToTable("Dnd5eRules");
        builder.HasKey(x => x.Index);
        builder.Property(x => x.Index).HasMaxLength(CatalogColumns.IndexMaxLength);
        builder.Property(x => x.Title).HasMaxLength(CatalogColumns.NameMaxLength).IsRequired();
        builder.HasIndex(x => x.Title);
        builder.Property(x => x.Category).HasMaxLength(RuleDefinition.CategoryMaxLength).IsRequired();
        builder.Property(x => x.Body).HasJsonListConversion();
        builder.Property(x => x.Tags).HasJsonListConversion();
        builder.Property(x => x.Source).HasMaxLength(CatalogSources.MaxLength).IsRequired();
        builder.HasIndex(x => x.Source);
    }
}

internal sealed class ReferenceEntryConfiguration : IEntityTypeConfiguration<ReferenceEntry>
{
    public void Configure(EntityTypeBuilder<ReferenceEntry> builder)
    {
        builder.ToTable("Dnd5eReferenceEntries");
        builder.HasKey(x => new { x.Kind, x.Index });
        builder.Property(x => x.Kind).HasMaxLength(ReferenceEntry.KindMaxLength);
        builder.Property(x => x.Index).HasMaxLength(CatalogColumns.IndexMaxLength);
        builder.Property(x => x.Name).HasMaxLength(CatalogColumns.NameMaxLength).IsRequired();
        builder.Property(x => x.DescriptionJson).IsRequired();
        builder.Property(x => x.Source).HasMaxLength(CatalogSources.MaxLength).IsRequired();
        builder.HasIndex(x => x.Source);
    }
}

internal sealed class SkillDefinitionConfiguration : IEntityTypeConfiguration<SkillDefinition>
{
    public void Configure(EntityTypeBuilder<SkillDefinition> builder)
    {
        builder.ToTable("CatalogSkills");
        builder.HasKey(x => x.Index);
        builder.Property(x => x.Index).HasMaxLength(CatalogColumns.IndexMaxLength);
        builder.Property(x => x.Name).HasMaxLength(CatalogColumns.NameMaxLength).IsRequired();
        builder.HasIndex(x => x.Name);
        builder.Property(x => x.AbilityIndex).HasMaxLength(8).IsRequired();
        builder.Property(x => x.Description).HasJsonListConversion();
    }
}

internal sealed class BackgroundDefinitionConfiguration : IEntityTypeConfiguration<BackgroundDefinition>
{
    public void Configure(EntityTypeBuilder<BackgroundDefinition> builder)
    {
        builder.ToTable("CatalogBackgrounds");
        builder.HasKey(x => x.Index);
        builder.Property(x => x.Index).HasMaxLength(CatalogColumns.IndexMaxLength);
        builder.Property(x => x.Name).HasMaxLength(CatalogColumns.NameMaxLength).IsRequired();
        builder.HasIndex(x => x.Name);
        builder.Property(x => x.FeatureName).HasMaxLength(CatalogColumns.NameMaxLength).IsRequired();
        builder.Property(x => x.FeatureDescription).HasJsonListConversion();
        builder.Property(x => x.SkillProficiencies).HasJsonListConversion();
        builder.Property(x => x.StartingEquipmentText).IsRequired();
        builder.Ignore(x => x.StartingEquipment);
        builder.Ignore(x => x.Choices);
        builder.Ignore(x => x.Personality);
        builder.Ignore(x => x.OptionalTables);
        builder.Property(x => x.Source).HasMaxLength(CatalogSources.MaxLength).IsRequired();
        builder.HasIndex(x => x.Source);
    }
}

internal sealed class EquipmentCategoryConfiguration : IEntityTypeConfiguration<EquipmentCategory>
{
    public void Configure(EntityTypeBuilder<EquipmentCategory> builder)
    {
        builder.ToTable("CatalogEquipmentCategories");
        builder.HasKey(x => x.Index);
        builder.Property(x => x.Index).HasMaxLength(CatalogColumns.IndexMaxLength);
        builder.Property(x => x.Name).HasMaxLength(CatalogColumns.NameMaxLength).IsRequired();
        builder.Property(x => x.ItemIndexes).HasJsonListConversion();
        builder.Property(x => x.Source).HasMaxLength(CatalogSources.MaxLength).IsRequired();
        builder.HasIndex(x => x.Source);
    }
}
