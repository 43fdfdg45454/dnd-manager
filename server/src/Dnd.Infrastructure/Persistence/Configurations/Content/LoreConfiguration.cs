using Dnd.Domain.Campaigns;
using Dnd.Domain.Files;
using Dnd.Domain.Lore;
using Dnd.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dnd.Infrastructure.Persistence.Configurations.Content;

internal sealed class LoreEntryConfiguration : IEntityTypeConfiguration<LoreEntry>
{
    public void Configure(EntityTypeBuilder<LoreEntry> builder)
    {
        builder.ToTable("LoreEntries");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Title).HasMaxLength(LoreEntry.TitleMaxLength).IsRequired();
        builder.Property(x => x.Slug).HasMaxLength(LoreSlug.MaxLength).IsRequired();
        builder.Property(x => x.Category).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(x => x.ContentMarkdown).IsRequired();
        builder.Property(x => x.Visibility).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt).IsRequired();

        builder.HasOne<Campaign>().WithMany().HasForeignKey(x => x.CampaignId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => new { x.CampaignId, x.Slug }).IsUnique();

        // Deleting an entry turns its children into root entries.
        builder.HasOne<LoreEntry>().WithMany().HasForeignKey(x => x.ParentId).IsRequired(false).OnDelete(DeleteBehavior.SetNull);
        builder.HasIndex(x => x.ParentId);

        builder.HasOne<StoredFile>().WithMany().HasForeignKey(x => x.CoverFileId).IsRequired(false).OnDelete(DeleteBehavior.NoAction);
        builder.HasIndex(x => x.CoverFileId);

        // Users are never deleted (they are deactivated).
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.CreatedByUserId);

        builder.HasMany(x => x.Attachments).WithOne().HasForeignKey(x => x.LoreEntryId).IsRequired().OnDelete(DeleteBehavior.Cascade);
        var navigation = builder.Metadata.FindNavigation(nameof(LoreEntry.Attachments))!;
        navigation.SetField("_attachments");
        navigation.SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class LoreAttachmentConfiguration : IEntityTypeConfiguration<LoreAttachment>
{
    public void Configure(EntityTypeBuilder<LoreAttachment> builder)
    {
        builder.ToTable("LoreAttachments");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Caption).HasMaxLength(LoreAttachment.CaptionMaxLength);
        builder.Property(x => x.CreatedAt).IsRequired();

        builder.HasIndex(x => x.LoreEntryId);
        builder.HasOne<StoredFile>().WithMany().HasForeignKey(x => x.FileId).OnDelete(DeleteBehavior.NoAction);
        builder.HasIndex(x => x.FileId);
    }
}
