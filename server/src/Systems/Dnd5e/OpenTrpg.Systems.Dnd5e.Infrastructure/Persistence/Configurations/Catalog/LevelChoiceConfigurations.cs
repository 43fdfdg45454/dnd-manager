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

// Level choice catalog (phase 16c). No foreign keys: content packs add options to sets and rules to classes of
// other sources, and every source replaces only its own rows.

internal sealed class OptionSetDefinitionConfiguration : IEntityTypeConfiguration<OptionSetDefinition>
{
    public void Configure(EntityTypeBuilder<OptionSetDefinition> builder)
    {
        builder.ToTable("CatalogOptionSets");
        builder.HasKey(x => x.SetId);
        builder.Property(x => x.SetId).HasMaxLength(CatalogColumns.IndexMaxLength);
        builder.Property(x => x.Name).HasMaxLength(CatalogColumns.NameMaxLength).IsRequired();
        builder.Property(x => x.Source).HasMaxLength(CatalogSources.MaxLength).IsRequired();
        builder.HasIndex(x => x.Source);
    }
}

internal sealed class OptionDefinitionConfiguration : IEntityTypeConfiguration<OptionDefinition>
{
    public void Configure(EntityTypeBuilder<OptionDefinition> builder)
    {
        builder.ToTable("CatalogOptions");
        builder.HasKey(x => x.Index);
        builder.Property(x => x.Index).HasMaxLength(CatalogColumns.IndexMaxLength);
        builder.Property(x => x.SetId).HasMaxLength(CatalogColumns.IndexMaxLength).IsRequired();
        builder.HasIndex(x => x.SetId);
        builder.Property(x => x.Name).HasMaxLength(CatalogColumns.NameMaxLength).IsRequired();
        builder.Property(x => x.Description).HasJsonListConversion();
        builder.Property(x => x.PrerequisitesText).HasMaxLength(LevelChoiceRule.NoteMaxLength);
        builder.Property(x => x.ModifiersJson).IsRequired();
        builder.Property(x => x.Source).HasMaxLength(CatalogSources.MaxLength).IsRequired();
        builder.HasIndex(x => x.Source);

        builder.Ignore(x => x.Prerequisites);
        builder.Ignore(x => x.Modifiers);
        builder.Ignore(x => x.AbilityIncrease);
        builder.Ignore(x => x.Grants);
        builder.Ignore(x => x.Resource);
        builder.Ignore(x => x.Cost);
    }
}

internal sealed class LevelChoiceRuleConfiguration : IEntityTypeConfiguration<LevelChoiceRule>
{
    public void Configure(EntityTypeBuilder<LevelChoiceRule> builder)
    {
        builder.ToTable("CatalogLevelChoiceRules");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasMaxLength(CatalogColumns.IndexMaxLength * 3 + 8);
        builder.Property(x => x.ClassIndex).HasMaxLength(CatalogColumns.IndexMaxLength).IsRequired();
        builder.Property(x => x.SubclassIndex).HasMaxLength(CatalogColumns.IndexMaxLength);
        builder.HasIndex(x => new { x.ClassIndex, x.Level });
        builder.Property(x => x.Key).HasMaxLength(LevelChoiceRule.KeyMaxLength).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(CatalogColumns.NameMaxLength).IsRequired();
        builder.Property(x => x.Kind).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(x => x.SetId).HasMaxLength(CatalogColumns.IndexMaxLength);
        builder.Property(x => x.Note).HasMaxLength(LevelChoiceRule.NoteMaxLength).IsRequired();
        builder.Property(x => x.After).HasMaxLength(LevelChoiceRule.KeyMaxLength);
        builder.Property(x => x.Source).HasMaxLength(CatalogSources.MaxLength).IsRequired();
        builder.HasIndex(x => x.Source);

        builder.Ignore(x => x.From);
        builder.Ignore(x => x.Filter);
    }
}
