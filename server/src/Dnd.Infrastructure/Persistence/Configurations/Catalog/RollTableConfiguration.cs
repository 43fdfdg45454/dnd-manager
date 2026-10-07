using Dnd.Domain.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dnd.Infrastructure.Persistence.Configurations.Catalog;

/// <summary>Generic roll tables of the content packs (phase 22). Class and subclass are resolved by index, without foreign keys.</summary>
internal sealed class RollTableConfiguration : IEntityTypeConfiguration<RollTable>
{
    public void Configure(EntityTypeBuilder<RollTable> builder)
    {
        builder.ToTable("CatalogRollTables");
        builder.HasKey(x => new { x.Source, x.Key });
        builder.Property(x => x.Source).HasMaxLength(CatalogSources.MaxLength);
        builder.Property(x => x.Key).HasMaxLength(RollTable.KeyMaxLength);
        builder.Property(x => x.Name).HasMaxLength(CatalogColumns.NameMaxLength).IsRequired();
        builder.Property(x => x.Dice).HasMaxLength(8).IsRequired();
        builder.Property(x => x.ClassIndex).HasMaxLength(CatalogColumns.IndexMaxLength);
        builder.Property(x => x.SubclassIndex).HasMaxLength(CatalogColumns.IndexMaxLength);
        builder.Property(x => x.EntriesJson).IsRequired();
        builder.Ignore(x => x.Entries);
        builder.HasIndex(x => x.SubclassIndex);
    }
}
