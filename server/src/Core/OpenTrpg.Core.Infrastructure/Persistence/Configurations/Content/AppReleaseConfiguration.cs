using OpenTrpg.Core.Domain.Files;
using OpenTrpg.Core.Domain.Releases;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace OpenTrpg.Core.Infrastructure.Persistence.Configurations.Content;

internal sealed class AppReleaseConfiguration : IEntityTypeConfiguration<AppRelease>
{
    public void Configure(EntityTypeBuilder<AppRelease> builder)
    {
        builder.ToTable("AppReleases");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Version).HasMaxLength(AppRelease.VersionMaxLength).IsRequired();
        builder.Property(x => x.BuildNumber).IsRequired();
        builder.Property(x => x.Notes).HasMaxLength(AppRelease.NotesMaxLength);
        builder.Property(x => x.IsMandatory).IsRequired();
        builder.Property(x => x.PublishedAt).IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();

        // The APK is deleted together with its release, never on its own.
        builder.HasOne<StoredFile>().WithMany().HasForeignKey(x => x.FileId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.FileId);

        builder.HasIndex(x => x.Version).IsUnique();
        builder.HasIndex(x => x.BuildNumber).IsUnique();
    }
}
