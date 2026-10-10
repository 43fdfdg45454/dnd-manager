using OpenTrpg.Core.Domain.Campaigns;
using OpenTrpg.Core.Domain.Sessions;
using OpenTrpg.Core.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace OpenTrpg.Core.Infrastructure.Persistence.Configurations;

internal sealed class CampaignConfiguration : IEntityTypeConfiguration<Campaign>
{
    public void Configure(EntityTypeBuilder<Campaign> builder)
    {
        builder.ToTable("Campaigns");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.Name).HasMaxLength(Campaign.NameMaxLength).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(Campaign.DescriptionMaxLength).IsRequired();
        builder.Property(x => x.SystemId).HasMaxLength(Campaign.SystemIdMaxLength).IsRequired().HasDefaultValue(Campaign.DefaultSystemId);
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt).IsRequired();
        builder.Property(x => x.TimeZoneId).HasMaxLength(CampaignSchedule.TimeZoneIdMaxLength).IsRequired();
        builder.Property(x => x.ReminderOffsetsMinutesJson).HasMaxLength(256).IsRequired();
        builder.Ignore(x => x.ReminderOffsetsMinutes);
        builder.Property(x => x.PlayersCanTakeFromStash).IsRequired();

        // Value-based optimistic concurrency: two stash gold changes computed from the same amount conflict (409).
        builder.Property(x => x.StashCopperPieces).IsRequired().IsConcurrencyToken();

        // Users are never deleted while they own campaigns (they are deactivated instead).
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.OwnerId);

        builder.HasMany(x => x.Members).WithOne().HasForeignKey(x => x.CampaignId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x => x.Members).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
