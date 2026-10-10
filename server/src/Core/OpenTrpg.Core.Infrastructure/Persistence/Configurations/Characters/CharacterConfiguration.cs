using OpenTrpg.Core.Domain.Campaigns;
using OpenTrpg.Core.Domain.Characters;
using OpenTrpg.Core.Domain.Files;
using OpenTrpg.Core.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace OpenTrpg.Core.Infrastructure.Persistence.Configurations.Characters;

internal sealed class CharacterConfiguration : IEntityTypeConfiguration<Character>
{
    public void Configure(EntityTypeBuilder<Character> builder)
    {
        builder.ToTable("Characters");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.Name).HasMaxLength(Character.NameMaxLength).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(x => x.Money).HasColumnName("CopperPieces");
        builder.Property(x => x.Notes).IsRequired();
        builder.Property(x => x.Backstory).IsRequired();
        builder.Property(x => x.PersonalityTraits).HasMaxLength(Character.PersonalityMaxLength).IsRequired();
        builder.Property(x => x.Ideals).HasMaxLength(Character.PersonalityMaxLength).IsRequired();
        builder.Property(x => x.Bonds).HasMaxLength(Character.PersonalityMaxLength).IsRequired();
        builder.Property(x => x.Flaws).HasMaxLength(Character.PersonalityMaxLength).IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt).IsRequired();

        // Bumped by the domain on every change; portable optimistic concurrency (no provider row version).
        builder.Property(x => x.Version).IsConcurrencyToken();

        builder.Ignore(x => x.AttunedCount);

        builder.HasOne<Campaign>().WithMany().HasForeignKey(x => x.CampaignId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => x.CampaignId);

        // Users are never deleted (they are deactivated); null owner = non-player character.
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.OwnerUserId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.OwnerUserId);

        // The portrait is an uploaded file; the application deletes it only once nothing refers to it.
        builder.HasOne<StoredFile>().WithMany().HasForeignKey(x => x.PortraitFileId).IsRequired(false).OnDelete(DeleteBehavior.NoAction);
        builder.HasIndex(x => x.PortraitFileId);

        builder.HasMany(x => x.Items)
            .WithOne()
            .HasForeignKey(x => x.CharacterId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);
        var navigation = builder.Metadata.FindNavigation(nameof(Character.Items))!;
        navigation.SetField("_items");
        navigation.SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class ChangeRequestConfiguration : IEntityTypeConfiguration<ChangeRequest>
{
    public void Configure(EntityTypeBuilder<ChangeRequest> builder)
    {
        builder.ToTable("ChangeRequests");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Type).HasMaxLength(ChangeRequestTypes.MaxLength).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(x => x.PayloadJson).IsRequired();
        builder.Property(x => x.Comment).HasMaxLength(ChangeRequest.CommentMaxLength);
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Ignore(x => x.IsPending);

        builder.HasOne<Campaign>().WithMany().HasForeignKey(x => x.CampaignId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => new { x.CampaignId, x.Status });

        builder.HasOne<Character>().WithMany().HasForeignKey(x => x.CharacterId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => new { x.CharacterId, x.Status });

        builder.HasOne<User>().WithMany().HasForeignKey(x => x.RequestedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.RequestedByUserId);
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.ResolvedByUserId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.ResolvedByUserId);
    }
}

internal sealed class RestRequestConfiguration : IEntityTypeConfiguration<RestRequest>
{
    public void Configure(EntityTypeBuilder<RestRequest> builder)
    {
        builder.ToTable("RestRequests");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Kind).HasMaxLength(RestRequest.KindMaxLength).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(x => x.PayloadJson).IsRequired();
        builder.Property(x => x.Comment).HasMaxLength(RestRequest.CommentMaxLength);
        builder.Property(x => x.RequestedAt).IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Ignore(x => x.IsPending);

        builder.HasOne<Campaign>().WithMany().HasForeignKey(x => x.CampaignId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => new { x.CampaignId, x.Status });

        // At most one pending request per character (filtered unique index; PostgreSQL and SQLite).
        builder.HasOne<Character>().WithMany().HasForeignKey(x => x.CharacterId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => x.CharacterId)
            .IsUnique()
            .HasFilter($"\"Status\" = '{nameof(RestRequestStatus.Pending)}'")
            .HasDatabaseName("IX_RestRequests_CharacterId_Pending");
        builder.HasIndex(x => new { x.CharacterId, x.Status });

        builder.HasOne<User>().WithMany().HasForeignKey(x => x.RequestedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.RequestedByUserId);
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.ResolvedByUserId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.ResolvedByUserId);
    }
}
