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
