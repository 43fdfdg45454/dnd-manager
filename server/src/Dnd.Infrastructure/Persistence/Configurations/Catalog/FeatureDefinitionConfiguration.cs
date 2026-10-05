using Dnd.Domain.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dnd.Infrastructure.Persistence.Configurations.Catalog;

internal sealed class FeatureDefinitionConfiguration : IEntityTypeConfiguration<FeatureDefinition>
{
    public void Configure(EntityTypeBuilder<FeatureDefinition> builder)
    {
        builder.ToTable("CatalogFeatures");
        builder.HasKey(x => x.Index);
        builder.Property(x => x.Index).HasMaxLength(CatalogColumns.IndexMaxLength);
        builder.Property(x => x.Name).HasMaxLength(CatalogColumns.NameMaxLength).IsRequired();
        builder.HasIndex(x => x.Name);
        builder.Property(x => x.ClassIndex).HasMaxLength(CatalogColumns.IndexMaxLength).IsRequired();
        builder.Property(x => x.SubclassIndex).HasMaxLength(CatalogColumns.IndexMaxLength);
        builder.HasIndex(x => new { x.ClassIndex, x.Level });
        builder.HasOne<ClassDefinition>().WithMany().HasForeignKey(x => x.ClassIndex).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<SubclassDefinition>().WithMany().HasForeignKey(x => x.SubclassIndex).OnDelete(DeleteBehavior.Cascade);

        builder.Property(x => x.Description).HasJsonListConversion();
    }
}
