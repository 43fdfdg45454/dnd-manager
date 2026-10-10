using OpenTrpg.Core.Domain.Campaigns;
using OpenTrpg.Core.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace OpenTrpg.Core.Infrastructure.Persistence.Configurations;

internal sealed class CampaignMemberConfiguration : IEntityTypeConfiguration<CampaignMember>
{
    public void Configure(EntityTypeBuilder<CampaignMember> builder)
    {
        builder.ToTable("CampaignMembers");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.Role).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(x => x.JoinedAt).IsRequired();

        // Also serves the access check (CampaignId, UserId) as a single index seek.
        builder.HasIndex(x => new { x.CampaignId, x.UserId }).IsUnique();
        builder.HasIndex(x => x.UserId);

        builder.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}
