using OpenTrpg.Core.Domain.Campaigns;
using OpenTrpg.Core.Domain.Catalog;
using OpenTrpg.Core.Domain.Characters;
using OpenTrpg.Core.Domain.Items;
using OpenTrpg.Core.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace OpenTrpg.Core.Infrastructure.Persistence.Configurations.Items;

// Template references use NoAction (checked at the end of the statement) rather than Restrict, so
// deleting a campaign can cascade over its homebrew templates and the entries that use them in the
// same statement. Deleting a template still in use is rejected by the application (409) beforehand.

internal sealed class CharacterItemConfiguration : IEntityTypeConfiguration<CharacterItem>
{
    public void Configure(EntityTypeBuilder<CharacterItem> builder)
    {
        builder.ToTable("CharacterItems");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Notes).HasMaxLength(ItemLimits.NotesMaxLength);
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt).IsRequired();
        builder.Ignore(x => x.HasCharges);
        builder.OwnsItemOverrides();

        // The relationship with Character (cascade) is configured by CharacterConfiguration.
        builder.HasIndex(x => x.CharacterId);
        builder.HasOne<Campaign>().WithMany().HasForeignKey(x => x.CampaignId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => x.CampaignId);
        builder.HasOne<ItemTemplate>().WithMany().HasForeignKey(x => x.TemplateId).IsRequired(false).OnDelete(DeleteBehavior.NoAction);
        builder.HasIndex(x => x.TemplateId);
    }
}

internal sealed class ShopConfiguration : IEntityTypeConfiguration<Shop>
{
    public void Configure(EntityTypeBuilder<Shop> builder)
    {
        builder.ToTable("Shops");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Name).HasMaxLength(Shop.NameMaxLength).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(Shop.DescriptionMaxLength);
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt).IsRequired();

        builder.HasOne<Campaign>().WithMany().HasForeignKey(x => x.CampaignId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => new { x.CampaignId, x.Name });

        builder.HasMany(x => x.Items).WithOne().HasForeignKey(x => x.ShopId).IsRequired().OnDelete(DeleteBehavior.Cascade);
        var navigation = builder.Metadata.FindNavigation(nameof(Shop.Items))!;
        navigation.SetField("_items");
        navigation.SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class ShopItemConfiguration : IEntityTypeConfiguration<ShopItem>
{
    public void Configure(EntityTypeBuilder<ShopItem> builder)
    {
        builder.ToTable("ShopItems");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Version).IsConcurrencyToken();
        builder.OwnsItemOverrides();

        builder.HasIndex(x => x.ShopId);
        builder.HasOne<ItemTemplate>().WithMany().HasForeignKey(x => x.TemplateId).IsRequired(false).OnDelete(DeleteBehavior.NoAction);
        builder.HasIndex(x => x.TemplateId);
    }
}

internal sealed class TransactionConfiguration : IEntityTypeConfiguration<Transaction>
{
    public void Configure(EntityTypeBuilder<Transaction> builder)
    {
        builder.ToTable("Transactions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Type).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(x => x.ItemName).HasMaxLength(ItemLimits.NameMaxLength).IsRequired();
        builder.Property(x => x.At).IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();

        builder.HasOne<Campaign>().WithMany().HasForeignKey(x => x.CampaignId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => x.CampaignId);
        builder.HasOne<Shop>().WithMany().HasForeignKey(x => x.ShopId).IsRequired(false).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => x.ShopId);
        builder.HasOne<Character>().WithMany().HasForeignKey(x => x.CharacterId).IsRequired(false).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => x.CharacterId);

        // Users are deactivated, never deleted while they have history.
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.ActorUserId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.ActorUserId);
    }
}

internal sealed class PartyStashItemConfiguration : IEntityTypeConfiguration<PartyStashItem>
{
    public void Configure(EntityTypeBuilder<PartyStashItem> builder)
    {
        builder.ToTable("PartyStashItems");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Notes).HasMaxLength(ItemLimits.NotesMaxLength);
        builder.Property(x => x.AddedAt).IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.Version).IsConcurrencyToken();
        builder.Ignore(x => x.HasCharges);
        builder.OwnsItemOverrides();

        builder.HasOne<Campaign>().WithMany().HasForeignKey(x => x.CampaignId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => x.CampaignId);
        builder.HasOne<ItemTemplate>().WithMany().HasForeignKey(x => x.TemplateId).IsRequired(false).OnDelete(DeleteBehavior.NoAction);
        builder.HasIndex(x => x.TemplateId);
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.AddedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.AddedByUserId);
    }
}
