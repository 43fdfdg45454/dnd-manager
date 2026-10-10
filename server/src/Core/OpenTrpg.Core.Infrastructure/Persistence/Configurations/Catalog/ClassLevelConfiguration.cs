using OpenTrpg.Core.Domain.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace OpenTrpg.Core.Infrastructure.Persistence.Configurations.Catalog;

internal sealed class ClassLevelConfiguration : IEntityTypeConfiguration<ClassLevel>
{
    public void Configure(EntityTypeBuilder<ClassLevel> builder)
    {
        builder.ToTable("CatalogClassLevels");
        builder.HasKey(x => x.Index);
        builder.Property(x => x.Index).HasMaxLength(CatalogColumns.IndexMaxLength);
        builder.Property(x => x.ClassIndex).HasMaxLength(CatalogColumns.IndexMaxLength).IsRequired();
        builder.HasIndex(x => new { x.ClassIndex, x.Level }).IsUnique();
        builder.HasOne<ClassDefinition>().WithMany().HasForeignKey(x => x.ClassIndex).OnDelete(DeleteBehavior.Cascade);

        builder.Property(x => x.FeatureIndexes).HasJsonListConversion();
        builder.Property(x => x.SpellSlots).HasJsonListConversion();
        builder.Property(x => x.ClassSpecificJson).IsRequired();
    }
}
