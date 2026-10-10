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

internal sealed class ClassDefinitionConfiguration : IEntityTypeConfiguration<ClassDefinition>
{
    public void Configure(EntityTypeBuilder<ClassDefinition> builder)
    {
        builder.ToTable("CatalogClasses");
        builder.HasKey(x => x.Index);
        builder.Property(x => x.Index).HasMaxLength(CatalogColumns.IndexMaxLength);
        builder.Property(x => x.Name).HasMaxLength(CatalogColumns.NameMaxLength).IsRequired();
        builder.HasIndex(x => x.Name);

        builder.Property(x => x.SavingThrows).HasJsonListConversion();
        builder.Property(x => x.ProficiencyNames).HasJsonListConversion();
        builder.Property(x => x.SpellcastingAbility).HasMaxLength(8);
        builder.Property(x => x.SubclassFlavor).HasMaxLength(CatalogColumns.NameMaxLength).IsRequired();
        builder.Property(x => x.StartingEquipmentText).IsRequired();
        builder.Property(x => x.SkillChoicesJson).IsRequired();
        builder.Ignore(x => x.StartingEquipment);
        builder.Property(x => x.Source).HasMaxLength(CatalogSources.MaxLength).IsRequired();
        builder.HasIndex(x => x.Source);
        builder.Property(x => x.Description).HasJsonListConversion();
        builder.Ignore(x => x.Spellcasting);
        builder.Ignore(x => x.Multiclassing);
        builder.Ignore(x => x.Resources);
        builder.Ignore(x => x.SpellList);
        builder.Ignore(x => x.PreparesSpells);
    }
}
