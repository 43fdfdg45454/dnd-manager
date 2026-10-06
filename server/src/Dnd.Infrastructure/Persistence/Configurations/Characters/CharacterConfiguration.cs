using Dnd.Domain.Campaigns;
using Dnd.Domain.Characters;
using Dnd.Domain.Files;
using Dnd.Domain.Items;
using Dnd.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dnd.Infrastructure.Persistence.Configurations.Characters;

internal sealed class CharacterConfiguration : IEntityTypeConfiguration<Character>
{
    public void Configure(EntityTypeBuilder<Character> builder)
    {
        builder.ToTable("Characters");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.Name).HasMaxLength(Character.NameMaxLength).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(x => x.RaceIndex).HasMaxLength(Character.IndexMaxLength);
        builder.Property(x => x.SubraceIndex).HasMaxLength(Character.IndexMaxLength);
        builder.Property(x => x.BackgroundIndex).HasMaxLength(Character.IndexMaxLength);
        builder.Property(x => x.Alignment).HasMaxLength(Character.AlignmentMaxLength);
        builder.Property(x => x.HpMode).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(x => x.ConditionsJson).IsRequired();
        builder.Property(x => x.ConcentratingOnSpellIndex).HasMaxLength(Character.IndexMaxLength);
        builder.Property(x => x.HitDiceUsedJson).IsRequired();
        builder.Property(x => x.Notes).IsRequired();
        builder.Property(x => x.Backstory).IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt).IsRequired();

        // Bumped by the domain on every change; portable optimistic concurrency (no provider row version).
        builder.Property(x => x.Version).IsConcurrencyToken();

        // Calculated (read-only) views over the stored data.
        builder.Ignore(x => x.OrderedClasses);
        builder.Ignore(x => x.BaseAbilities);
        builder.Ignore(x => x.TotalLevel);
        builder.Ignore(x => x.Conditions);
        builder.Ignore(x => x.HitDiceUsed);
        builder.Ignore(x => x.AttunedCount);

        builder.HasOne<Campaign>().WithMany().HasForeignKey(x => x.CampaignId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => x.CampaignId);

        // Users are never deleted (they are deactivated); null owner = non-player character.
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.OwnerUserId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.OwnerUserId);

        // The portrait is an uploaded file; the application deletes it only once nothing refers to it.
        builder.HasOne<StoredFile>().WithMany().HasForeignKey(x => x.PortraitFileId).IsRequired(false).OnDelete(DeleteBehavior.NoAction);
        builder.HasIndex(x => x.PortraitFileId);

        // Level-up granted by a DM (phase 16b); the granting user is kept like the resolver of a request.
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.LevelGrantedByUserId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.LevelGrantedByUserId);

        ConfigureChildren<CharacterClassLevel>(builder, nameof(Character.Classes), "_classes");
        ConfigureChildren<CharacterProficiency>(builder, nameof(Character.Proficiencies), "_proficiencies");
        ConfigureChildren<CharacterSpell>(builder, nameof(Character.Spells), "_spells");
        ConfigureChildren<SpellSlotState>(builder, nameof(Character.SpellSlots), "_spellSlots");
        ConfigureChildren<CharacterResource>(builder, nameof(Character.Resources), "_resources");
        ConfigureChildren<CharacterOverride>(builder, nameof(Character.Overrides), "_overrides");
        ConfigureChildren<CharacterItem>(builder, nameof(Character.Items), "_items");
    }

    private static void ConfigureChildren<TChild>(EntityTypeBuilder<Character> builder, string navigationName, string fieldName)
        where TChild : class
    {
        builder.HasMany<TChild>(navigationName)
            .WithOne()
            .HasForeignKey("CharacterId")
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        var navigation = builder.Metadata.FindNavigation(navigationName)!;
        navigation.SetField(fieldName);
        navigation.SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class CharacterClassLevelConfiguration : IEntityTypeConfiguration<CharacterClassLevel>
{
    public void Configure(EntityTypeBuilder<CharacterClassLevel> builder)
    {
        builder.ToTable("CharacterClassLevels");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.ClassIndex).HasMaxLength(Character.IndexMaxLength).IsRequired();
        builder.Property(x => x.SubclassIndex).HasMaxLength(Character.IndexMaxLength);
        builder.HasIndex(x => new { x.CharacterId, x.ClassIndex }).IsUnique();
    }
}

internal sealed class CharacterProficiencyConfiguration : IEntityTypeConfiguration<CharacterProficiency>
{
    public void Configure(EntityTypeBuilder<CharacterProficiency> builder)
    {
        builder.ToTable("CharacterProficiencies");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Type).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(x => x.Key).HasMaxLength(Character.IndexMaxLength).IsRequired();
        builder.Property(x => x.Source).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.HasIndex(x => new { x.CharacterId, x.Type, x.Key }).IsUnique();
    }
}

internal sealed class CharacterSpellConfiguration : IEntityTypeConfiguration<CharacterSpell>
{
    public void Configure(EntityTypeBuilder<CharacterSpell> builder)
    {
        builder.ToTable("CharacterSpells");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.SpellIndex).HasMaxLength(Character.IndexMaxLength).IsRequired();
        builder.Property(x => x.ClassIndex).HasMaxLength(Character.IndexMaxLength).IsRequired();
        builder.HasIndex(x => new { x.CharacterId, x.SpellIndex, x.ClassIndex }).IsUnique();
    }
}

internal sealed class SpellSlotStateConfiguration : IEntityTypeConfiguration<SpellSlotState>
{
    public void Configure(EntityTypeBuilder<SpellSlotState> builder)
    {
        builder.ToTable("CharacterSpellSlots");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.HasIndex(x => new { x.CharacterId, x.Level }).IsUnique();
    }
}

internal sealed class CharacterResourceConfiguration : IEntityTypeConfiguration<CharacterResource>
{
    public void Configure(EntityTypeBuilder<CharacterResource> builder)
    {
        builder.ToTable("CharacterResources");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Key).HasMaxLength(Character.IndexMaxLength);
        builder.Property(x => x.Name).HasMaxLength(CharacterResource.NameMaxLength).IsRequired();
        builder.Property(x => x.Recharge).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Ignore(x => x.Remaining);
        builder.HasIndex(x => x.CharacterId);
    }
}

internal sealed class CharacterOverrideConfiguration : IEntityTypeConfiguration<CharacterOverride>
{
    public void Configure(EntityTypeBuilder<CharacterOverride> builder)
    {
        builder.ToTable("CharacterOverrides");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Field).HasMaxLength(Character.IndexMaxLength + 16).IsRequired();
        builder.Property(x => x.Note).HasMaxLength(CharacterOverride.NoteMaxLength);
        builder.HasIndex(x => new { x.CharacterId, x.Field }).IsUnique();
    }
}

internal sealed class ChangeRequestConfiguration : IEntityTypeConfiguration<ChangeRequest>
{
    public void Configure(EntityTypeBuilder<ChangeRequest> builder)
    {
        builder.ToTable("ChangeRequests");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Type).HasConversion<string>().HasMaxLength(16).IsRequired();
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
        builder.Property(x => x.Kind).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(x => x.HitDiceJson).IsRequired();
        builder.Property(x => x.Comment).HasMaxLength(RestRequest.CommentMaxLength);
        builder.Property(x => x.RequestedAt).IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Ignore(x => x.IsPending);
        builder.Ignore(x => x.HitDice);

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
