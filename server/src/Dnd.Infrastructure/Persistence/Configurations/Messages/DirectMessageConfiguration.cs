using Dnd.Domain.Campaigns;
using Dnd.Domain.Characters;
using Dnd.Domain.Messages;
using Dnd.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dnd.Infrastructure.Persistence.Configurations.Messages;

internal sealed class DirectMessageConfiguration : IEntityTypeConfiguration<DirectMessage>
{
    public void Configure(EntityTypeBuilder<DirectMessage> builder)
    {
        builder.ToTable("DirectMessages");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Body).HasMaxLength(DirectMessage.BodyMaxLength).IsRequired();
        builder.Property(x => x.SentAt).IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Ignore(x => x.IsRead);

        builder.HasOne<Campaign>().WithMany().HasForeignKey(x => x.CampaignId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Character>().WithMany().HasForeignKey(x => x.CharacterId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => x.CharacterId);

        // Users are deactivated, never deleted while they have history.
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.SenderUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.RecipientUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.RecipientUserId);

        // Inbox (and unread count) of a player, and sent messages of a DM.
        builder.HasIndex(x => new { x.CampaignId, x.RecipientUserId, x.ReadAt });
        builder.HasIndex(x => new { x.CampaignId, x.SenderUserId });
    }
}
