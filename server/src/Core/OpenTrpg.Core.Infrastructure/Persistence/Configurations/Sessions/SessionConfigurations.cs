using OpenTrpg.Core.Domain.Campaigns;
using OpenTrpg.Core.Domain.Sessions;
using OpenTrpg.Core.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace OpenTrpg.Core.Infrastructure.Persistence.Configurations.Sessions;

internal sealed class GameSessionConfiguration : IEntityTypeConfiguration<GameSession>
{
    public void Configure(EntityTypeBuilder<GameSession> builder)
    {
        builder.ToTable("GameSessions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Number).IsRequired();
        builder.Property(x => x.Title).HasMaxLength(GameSession.TitleMaxLength).IsRequired();
        builder.Property(x => x.StartsAt).IsRequired();
        builder.Property(x => x.Location).HasMaxLength(GameSession.LocationMaxLength);
        builder.Property(x => x.Notes);
        builder.Property(x => x.SummaryMarkdown);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt).IsRequired();

        builder.HasOne<Campaign>().WithMany().HasForeignKey(x => x.CampaignId).OnDelete(DeleteBehavior.Cascade);

        // Correlative per campaign. The unique index is the safety net for concurrent creations.
        builder.HasIndex(x => new { x.CampaignId, x.Number }).IsUnique();

        // Users are never deleted (they are deactivated).
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.CreatedByUserId);

        builder.HasMany(x => x.Rsvps).WithOne().HasForeignKey(x => x.SessionId).IsRequired().OnDelete(DeleteBehavior.Cascade);
        var rsvps = builder.Metadata.FindNavigation(nameof(GameSession.Rsvps))!;
        rsvps.SetField("_rsvps");
        rsvps.SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(x => x.Reminders).WithOne().HasForeignKey(x => x.SessionId).IsRequired().OnDelete(DeleteBehavior.Cascade);
        var reminders = builder.Metadata.FindNavigation(nameof(GameSession.Reminders))!;
        reminders.SetField("_reminders");
        reminders.SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class SessionRsvpConfiguration : IEntityTypeConfiguration<SessionRsvp>
{
    public void Configure(EntityTypeBuilder<SessionRsvp> builder)
    {
        builder.ToTable("SessionRsvps");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(x => x.Comment).HasMaxLength(SessionRsvp.CommentMaxLength);
        builder.Property(x => x.UpdatedAt).IsRequired();

        builder.HasIndex(x => new { x.SessionId, x.UserId }).IsUnique();

        // Users are never deleted (they are deactivated).
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => x.UserId);
    }
}

internal sealed class ReminderConfiguration : IEntityTypeConfiguration<Reminder>
{
    public void Configure(EntityTypeBuilder<Reminder> builder)
    {
        builder.ToTable("Reminders");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.OffsetMinutes).IsRequired();
        builder.Property(x => x.SendAt).IsRequired();
        builder.Property(x => x.Attempts).IsRequired();
        builder.Property(x => x.LastError).HasMaxLength(Reminder.LastErrorMaxLength);

        builder.Ignore(x => x.IsPending);

        builder.HasIndex(x => x.SessionId);

        // The dispatcher looks for unsent, not abandoned reminders by send time.
        builder.HasIndex(x => new { x.SentAt, x.FailedAt, x.SendAt });
    }
}
