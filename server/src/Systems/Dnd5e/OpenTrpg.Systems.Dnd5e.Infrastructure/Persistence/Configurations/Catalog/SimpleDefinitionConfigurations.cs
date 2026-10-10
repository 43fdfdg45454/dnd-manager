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
