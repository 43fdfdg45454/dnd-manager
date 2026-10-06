using Dnd.Domain.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dnd.Infrastructure.Persistence.Configurations.Catalog;

internal sealed class SpellDefinitionConfiguration : IEntityTypeConfiguration<SpellDefinition>
{
    public void Configure(EntityTypeBuilder<SpellDefinition> builder)
    {
        builder.ToTable("CatalogSpells");
        builder.HasKey(x => x.Index);
        builder.Property(x => x.Source).HasMaxLength(CatalogSources.MaxLength).IsRequired();
        builder.HasIndex(x => x.Source);
        builder.Property(x => x.Index).HasMaxLength(CatalogColumns.IndexMaxLength);
        builder.Property(x => x.Name).HasMaxLength(CatalogColumns.NameMaxLength).IsRequired();
        builder.HasIndex(x => x.Name);
        builder.HasIndex(x => x.Level);

        builder.Property(x => x.School).HasMaxLength(CatalogColumns.ShortTextMaxLength).IsRequired();
        builder.Property(x => x.CastingTime).HasMaxLength(CatalogColumns.ShortTextMaxLength).IsRequired();
        builder.Property(x => x.Range).HasMaxLength(CatalogColumns.ShortTextMaxLength).IsRequired();
        builder.Property(x => x.Duration).HasMaxLength(CatalogColumns.ShortTextMaxLength).IsRequired();
        builder.Property(x => x.Components).HasJsonListConversion();
        builder.Property(x => x.Description).HasJsonListConversion();
        builder.Property(x => x.HigherLevel).HasJsonListConversion();
        builder.Property(x => x.ClassIndexes).HasJsonListConversion();
        builder.Property(x => x.SubclassIndexes).HasJsonListConversion();
        builder.Property(x => x.AttackType).HasMaxLength(16);
        builder.Property(x => x.DcAbility).HasMaxLength(8);
        builder.Property(x => x.Category).HasConversion<string>().HasMaxLength(SpellCategories.MaxLength).IsRequired();
    }
}
