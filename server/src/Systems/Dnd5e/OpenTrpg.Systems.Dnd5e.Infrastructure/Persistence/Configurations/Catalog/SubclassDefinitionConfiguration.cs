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

internal sealed class SubclassDefinitionConfiguration : IEntityTypeConfiguration<SubclassDefinition>
{
    public void Configure(EntityTypeBuilder<SubclassDefinition> builder)
    {
        builder.ToTable("CatalogSubclasses");
        builder.HasKey(x => x.Index);
        builder.Property(x => x.Source).HasMaxLength(CatalogSources.MaxLength).IsRequired();
        builder.HasIndex(x => x.Source);
        builder.Property(x => x.Index).HasMaxLength(CatalogColumns.IndexMaxLength);
        builder.Property(x => x.Name).HasMaxLength(CatalogColumns.NameMaxLength).IsRequired();
        builder.HasIndex(x => x.Name);
        builder.Property(x => x.ClassIndex).HasMaxLength(CatalogColumns.IndexMaxLength).IsRequired();
        builder.HasIndex(x => x.ClassIndex);
        builder.HasOne<ClassDefinition>().WithMany().HasForeignKey(x => x.ClassIndex).OnDelete(DeleteBehavior.Cascade);

        builder.Property(x => x.Flavor).HasMaxLength(CatalogColumns.NameMaxLength).IsRequired();
        builder.Property(x => x.Description).HasJsonListConversion();
        builder.Ignore(x => x.Spellcasting);
        builder.Ignore(x => x.ExpandedSpellList);
        builder.Ignore(x => x.ExpandedSpellIndexes);
    }
}
