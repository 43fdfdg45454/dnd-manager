using Dnd.Domain.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dnd.Infrastructure.Persistence.Configurations.Catalog;

internal sealed class SubclassLevelConfiguration : IEntityTypeConfiguration<SubclassLevel>
{
    public void Configure(EntityTypeBuilder<SubclassLevel> builder)
    {
        builder.ToTable("CatalogSubclassLevels");
        builder.HasKey(x => x.Index);
        builder.Property(x => x.Source).HasMaxLength(CatalogSources.MaxLength).IsRequired();
        builder.HasIndex(x => x.Source);
        builder.Property(x => x.Index).HasMaxLength(CatalogColumns.IndexMaxLength);
        builder.Property(x => x.SubclassIndex).HasMaxLength(CatalogColumns.IndexMaxLength).IsRequired();
        builder.HasIndex(x => new { x.SubclassIndex, x.Level }).IsUnique();
        builder.HasOne<SubclassDefinition>().WithMany().HasForeignKey(x => x.SubclassIndex).OnDelete(DeleteBehavior.Cascade);

        builder.Property(x => x.FeatureIndexes).HasJsonListConversion();
        builder.Ignore(x => x.Grants);
    }
}
