using Dnd.Domain.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dnd.Infrastructure.Persistence.Configurations.Catalog;

internal sealed class TraitDefinitionConfiguration : IEntityTypeConfiguration<TraitDefinition>
{
    public void Configure(EntityTypeBuilder<TraitDefinition> builder)
    {
        builder.ToTable("CatalogTraits");
        builder.HasKey(x => x.Index);
        builder.Property(x => x.Index).HasMaxLength(CatalogColumns.IndexMaxLength);
        builder.Property(x => x.Name).HasMaxLength(CatalogColumns.NameMaxLength).IsRequired();
        builder.HasIndex(x => x.Name);

        builder.Property(x => x.Description).HasJsonListConversion();
        builder.Property(x => x.RaceIndexes).HasJsonListConversion();
        builder.Property(x => x.SubraceIndexes).HasJsonListConversion();
    }
}
