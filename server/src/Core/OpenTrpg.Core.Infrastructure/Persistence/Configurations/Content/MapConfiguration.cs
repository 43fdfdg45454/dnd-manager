using OpenTrpg.Core.Domain.Campaigns;
using OpenTrpg.Core.Domain.Files;
using OpenTrpg.Core.Domain.Lore;
using OpenTrpg.Core.Domain.Maps;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace OpenTrpg.Core.Infrastructure.Persistence.Configurations.Content;

internal sealed class MapConfiguration : IEntityTypeConfiguration<Map>
{
    public void Configure(EntityTypeBuilder<Map> builder)
    {
        builder.ToTable("Maps");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Name).HasMaxLength(Map.NameMaxLength).IsRequired();
        builder.Property(x => x.Visibility).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt).IsRequired();

        builder.HasOne<Campaign>().WithMany().HasForeignKey(x => x.CampaignId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => x.CampaignId);

        builder.HasOne<StoredFile>().WithMany().HasForeignKey(x => x.FileId).OnDelete(DeleteBehavior.NoAction);
        builder.HasIndex(x => x.FileId);

        builder.HasMany(x => x.Pins).WithOne().HasForeignKey(x => x.MapId).IsRequired().OnDelete(DeleteBehavior.Cascade);
        var navigation = builder.Metadata.FindNavigation(nameof(Map.Pins))!;
        navigation.SetField("_pins");
        navigation.SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class MapPinConfiguration : IEntityTypeConfiguration<MapPin>
{
    public void Configure(EntityTypeBuilder<MapPin> builder)
    {
        builder.ToTable("MapPins");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Title).HasMaxLength(MapPin.TitleMaxLength).IsRequired();
        builder.Property(x => x.Note).IsRequired();
        builder.Property(x => x.Icon).HasMaxLength(16).IsRequired();
        builder.Property(x => x.Color).HasMaxLength(MapPin.ColorMaxLength);
        builder.Property(x => x.Visibility).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();

        builder.HasIndex(x => x.MapId);

        // Deleting the lore entry only unlinks the pins.
        builder.HasOne<LoreEntry>().WithMany().HasForeignKey(x => x.LoreEntryId).IsRequired(false).OnDelete(DeleteBehavior.SetNull);
        builder.HasIndex(x => x.LoreEntryId);
    }
}
