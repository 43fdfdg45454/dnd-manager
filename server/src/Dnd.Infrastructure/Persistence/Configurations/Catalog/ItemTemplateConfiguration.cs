using Dnd.Domain.Campaigns;
using Dnd.Domain.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dnd.Infrastructure.Persistence.Configurations.Catalog;

internal sealed class ItemTemplateConfiguration : IEntityTypeConfiguration<ItemTemplate>
{
    public void Configure(EntityTypeBuilder<ItemTemplate> builder)
    {
        builder.ToTable("ItemTemplates");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        // Homebrew items (with a campaign) are deleted together with their campaign.
        builder.HasOne<Campaign>().WithMany().HasForeignKey(x => x.CampaignId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => new { x.CampaignId, x.Name });

        // Only SRD items have an index; several NULLs are allowed by both PostgreSQL and SQLite.
        builder.Property(x => x.Index).HasMaxLength(CatalogColumns.IndexMaxLength);
        builder.HasIndex(x => x.Index).IsUnique();

        builder.Property(x => x.Name).HasMaxLength(ItemTemplate.NameMaxLength).IsRequired();
        builder.HasIndex(x => x.Name);
        builder.Property(x => x.Category).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.HasIndex(x => x.Category);
        builder.Property(x => x.Subcategory).HasMaxLength(CatalogColumns.ShortTextMaxLength).IsRequired();
        builder.Property(x => x.Rarity).HasConversion<string>().HasMaxLength(16);
        builder.Property(x => x.WeightLb).HasPrecision(10, 2);
        builder.Property(x => x.DamageDice).HasMaxLength(32);
        builder.Property(x => x.DamageType).HasMaxLength(32);
        builder.Property(x => x.VersatileDice).HasMaxLength(32);
        builder.Property(x => x.Properties).HasJsonListConversion();
        builder.Property(x => x.Description).HasJsonListConversion();
        builder.Property(x => x.Effects).HasJsonListConversion();
        builder.Property(x => x.Modifiers).HasJsonListConversion();
        builder.Property(x => x.CreatedAt).IsRequired();

        builder.Ignore(x => x.IsSrd);
    }
}
