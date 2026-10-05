using Dnd.Domain.Campaigns;
using Dnd.Domain.Files;
using Dnd.Domain.Library;
using Dnd.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dnd.Infrastructure.Persistence.Configurations.Content;

internal sealed class LibraryDocumentConfiguration : IEntityTypeConfiguration<LibraryDocument>
{
    public void Configure(EntityTypeBuilder<LibraryDocument> builder)
    {
        builder.ToTable("LibraryDocuments");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Title).HasMaxLength(LibraryDocument.TitleMaxLength).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(LibraryDocument.DescriptionMaxLength);
        builder.Property(x => x.Category).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();

        builder.HasOne<StoredFile>().WithMany().HasForeignKey(x => x.FileId).OnDelete(DeleteBehavior.NoAction);
        builder.HasIndex(x => x.FileId);

        // Users are never deleted (they are deactivated); null uploader = system document.
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.UploadedByUserId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.UploadedByUserId);
    }
}

internal sealed class CampaignDocumentConfiguration : IEntityTypeConfiguration<CampaignDocument>
{
    public void Configure(EntityTypeBuilder<CampaignDocument> builder)
    {
        builder.ToTable("CampaignDocuments");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Note).HasMaxLength(CampaignDocument.NoteMaxLength);
        builder.Property(x => x.CreatedAt).IsRequired();

        builder.HasOne<Campaign>().WithMany().HasForeignKey(x => x.CampaignId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<LibraryDocument>().WithMany().HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => new { x.CampaignId, x.DocumentId }).IsUnique();
        builder.HasIndex(x => x.DocumentId);
    }
}
