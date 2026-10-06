using Dnd.Domain.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dnd.Infrastructure.Persistence.Configurations.Catalog;

internal sealed class RaceDefinitionConfiguration : IEntityTypeConfiguration<RaceDefinition>
{
    public void Configure(EntityTypeBuilder<RaceDefinition> builder)
    {
        builder.ToTable("CatalogRaces");
        builder.HasKey(x => x.Index);
        builder.Property(x => x.Source).HasMaxLength(CatalogSources.MaxLength).IsRequired();
        builder.HasIndex(x => x.Source);
        builder.Property(x => x.Index).HasMaxLength(CatalogColumns.IndexMaxLength);
        builder.Property(x => x.Name).HasMaxLength(CatalogColumns.NameMaxLength).IsRequired();
        builder.HasIndex(x => x.Name);

        builder.Property(x => x.Size).HasMaxLength(CatalogColumns.ShortTextMaxLength).IsRequired();
        builder.Property(x => x.AbilityBonusesJson).IsRequired();
        builder.Property(x => x.TraitIndexes).HasJsonListConversion();
        builder.Property(x => x.Languages).HasJsonListConversion();
        builder.Property(x => x.SubraceIndexes).HasJsonListConversion();
        builder.Property(x => x.Age).IsRequired();
        builder.Property(x => x.Alignment).IsRequired();
        builder.Property(x => x.SizeDescription).IsRequired();
    }
}
