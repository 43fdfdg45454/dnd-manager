using OpenTrpg.Core.Domain.Campaigns;
using OpenTrpg.Core.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace OpenTrpg.Core.Infrastructure.Persistence.Configurations;

internal sealed class CampaignInvitationConfiguration : IEntityTypeConfiguration<CampaignInvitation>
{
    public void Configure(EntityTypeBuilder<CampaignInvitation> builder)
    {
        builder.ToTable("CampaignInvitations");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.Role).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();

        builder.HasOne<Campaign>().WithMany().HasForeignKey(x => x.CampaignId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => new { x.CampaignId, x.UserId }).IsUnique();

        builder.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => x.UserId);
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.InvitedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.InvitedByUserId);
    }
}
