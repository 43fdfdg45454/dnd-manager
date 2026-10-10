using OpenTrpg.Core.Domain.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace OpenTrpg.Core.Infrastructure.Persistence.Configurations.Catalog;

internal sealed class RaceExtensionDefinitionConfiguration : IEntityTypeConfiguration<RaceExtensionDefinition>
{
    public void Configure(EntityTypeBuilder<RaceExtensionDefinition> builder)
    {
        builder.ToTable("CatalogRaceExtensions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasMaxLength(CatalogSources.MaxLength + 1 + CatalogColumns.IndexMaxLength);
        builder.Property(x => x.Source).HasMaxLength(CatalogSources.MaxLength).IsRequired();
        builder.HasIndex(x => x.Source);
        builder.Property(x => x.RaceIndex).HasMaxLength(CatalogColumns.IndexMaxLength).IsRequired();
        builder.HasIndex(x => x.RaceIndex);
        builder.HasOne<RaceDefinition>().WithMany().HasForeignKey(x => x.RaceIndex).OnDelete(DeleteBehavior.Cascade);
        builder.Property(x => x.TraitIndexes).HasJsonListConversion();
        builder.Ignore(x => x.Grants);
        builder.Ignore(x => x.HeightWeight);
    }
}
