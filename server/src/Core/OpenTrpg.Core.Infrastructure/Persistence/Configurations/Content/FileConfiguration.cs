using OpenTrpg.Core.Domain.Campaigns;
using OpenTrpg.Core.Domain.Files;
using OpenTrpg.Core.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace OpenTrpg.Core.Infrastructure.Persistence.Configurations.Content;

// References to StoredFile (maps, lore, library) use NoAction (checked at the end of the statement)
// so deleting a campaign can cascade over its files and the rows that use them in one statement.
// The application removes a file only once nothing refers to it.

internal sealed class StoredFileConfiguration : IEntityTypeConfiguration<StoredFile>
{
    public void Configure(EntityTypeBuilder<StoredFile> builder)
    {
        builder.ToTable("StoredFiles");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.FileName).HasMaxLength(StoredFile.FileNameMaxLength).IsRequired();
        builder.Property(x => x.ContentType).HasMaxLength(StoredFile.ContentTypeMaxLength).IsRequired();
        builder.Property(x => x.Sha256).HasMaxLength(StoredFile.Sha256Length).IsRequired();
        builder.Property(x => x.Kind).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(x => x.StoragePath).HasMaxLength(StoredFile.StoragePathMaxLength).IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Ignore(x => x.IsImage);

        // Null campaign = global file (library, app releases).
        builder.HasOne<Campaign>().WithMany().HasForeignKey(x => x.CampaignId).IsRequired(false).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => x.CampaignId);

        // Users are never deleted (they are deactivated); null owner = registered by the system.
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.OwnerUserId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.OwnerUserId);

        builder.HasIndex(x => x.StoragePath).IsUnique();
    }
}
