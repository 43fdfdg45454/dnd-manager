using OpenTrpg.Core.Domain.Campaigns;
using OpenTrpg.Core.Domain.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace OpenTrpg.Core.Infrastructure.Persistence.Configurations.Catalog;

internal sealed class ContentPackConfiguration : IEntityTypeConfiguration<ContentPack>
{
    public void Configure(EntityTypeBuilder<ContentPack> builder)
    {
        builder.ToTable("ContentPacks");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasMaxLength(ContentPack.IdMaxLength).ValueGeneratedNever();
        builder.Property(x => x.SystemId).HasMaxLength(Campaign.SystemIdMaxLength).IsRequired();
        builder.HasIndex(x => x.SystemId);
        builder.Property(x => x.Name).HasMaxLength(ContentPack.NameMaxLength).IsRequired();
        builder.Property(x => x.Version).HasMaxLength(ContentPack.VersionMaxLength).IsRequired();
        builder.Property(x => x.FormatVersion).IsRequired();
        builder.Property(x => x.IsBase).IsRequired();
        builder.Property(x => x.ImportedAt).IsRequired();
        builder.Property(x => x.CountsJson).IsRequired();
        builder.Property(x => x.Requires).HasJsonListConversion();
    }
}

internal sealed class CampaignContentPackConfiguration : IEntityTypeConfiguration<CampaignContentPack>
{
    public void Configure(EntityTypeBuilder<CampaignContentPack> builder)
    {
        builder.ToTable("CampaignContentPacks");
        builder.HasKey(x => new { x.CampaignId, x.PackId });
        builder.Property(x => x.PackId).HasMaxLength(ContentPack.IdMaxLength);
        builder.HasIndex(x => x.PackId);
        builder.HasOne<Campaign>().WithMany().HasForeignKey(x => x.CampaignId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<ContentPack>().WithMany().HasForeignKey(x => x.PackId).OnDelete(DeleteBehavior.Cascade);
        builder.Property(x => x.EnabledAt).IsRequired();
        builder.Property(x => x.EnabledByUserId).IsRequired();
    }
}
