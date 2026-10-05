using Dnd.Domain.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dnd.Infrastructure.Persistence.Configurations.Catalog;

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
    }
}
