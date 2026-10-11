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

/// <summary>Item descriptions overridden by content packs (<c>itemOverrides</c>). No foreign key to the items: they are resolved by index.</summary>
internal sealed class ItemDescriptionOverrideConfiguration : IEntityTypeConfiguration<ItemDescriptionOverride>
{
    public void Configure(EntityTypeBuilder<ItemDescriptionOverride> builder)
    {
        builder.ToTable("CatalogItemDescriptionOverrides");
        builder.HasKey(x => new { x.Source, x.ItemIndex });
        builder.Property(x => x.Source).HasMaxLength(CatalogSources.MaxLength);
        builder.Property(x => x.ItemIndex).HasMaxLength(CatalogColumns.IndexMaxLength);
        builder.HasIndex(x => x.ItemIndex);
        builder.Property(x => x.Description).HasJsonListConversion();
        builder.Property(x => x.OriginalDescription).HasJsonListConversion();
        builder.Property(x => x.ImportedAt).IsRequired();
    }
}
