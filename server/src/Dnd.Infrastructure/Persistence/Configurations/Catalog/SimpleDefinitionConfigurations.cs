using Dnd.Domain.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dnd.Infrastructure.Persistence.Configurations.Catalog;

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
    }
}

internal sealed class CatalogImportConfiguration : IEntityTypeConfiguration<CatalogImport>
{
    public void Configure(EntityTypeBuilder<CatalogImport> builder)
    {
        builder.ToTable("CatalogImports");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Ruleset).HasMaxLength(32).IsRequired();
        builder.Property(x => x.DatasetVersion).HasMaxLength(100).IsRequired();
        builder.HasIndex(x => new { x.Ruleset, x.DatasetVersion }).IsUnique();
        builder.Property(x => x.ImportedAt).IsRequired();
        builder.Property(x => x.CountsJson).IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();
    }
}
