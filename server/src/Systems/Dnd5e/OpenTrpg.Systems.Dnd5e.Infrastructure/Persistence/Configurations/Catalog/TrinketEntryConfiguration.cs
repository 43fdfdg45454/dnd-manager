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

/// <summary>Trinket table of the content packs (phase 21). No foreign key to the items: they are resolved by index.</summary>
internal sealed class TrinketEntryConfiguration : IEntityTypeConfiguration<TrinketEntry>
{
    public void Configure(EntityTypeBuilder<TrinketEntry> builder)
    {
        builder.ToTable("CatalogTrinkets");
        builder.HasKey(x => new { x.Source, x.Roll });
        builder.Property(x => x.Source).HasMaxLength(CatalogSources.MaxLength);
        builder.Property(x => x.ItemIndex).HasMaxLength(CatalogColumns.IndexMaxLength).IsRequired();
    }
}
