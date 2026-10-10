using OpenTrpg.Core.Domain.Catalog;
using OpenTrpg.Core.Domain.Items;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace OpenTrpg.Core.Infrastructure.Persistence.Configurations.Items;

/// <summary>
/// Maps <see cref="ItemOverrides"/> as an owned type stored in its owner's table, every column
/// prefixed with "Override". The navigation is required so an entry without overrides still gets an
/// (empty) instance instead of null.
/// </summary>
internal static class ItemOverridesMapping
{
    public const string ColumnPrefix = "Override";

    public static void OwnsItemOverrides<TOwner>(this EntityTypeBuilder<TOwner> builder)
        where TOwner : class
    {
        builder.OwnsOne<ItemOverrides>("Overrides", o =>
        {
            o.Property(x => x.Name).HasColumnName(ColumnPrefix + nameof(ItemOverrides.Name)).HasMaxLength(ItemLimits.NameMaxLength);
            o.Property(x => x.Description).HasColumnName(ColumnPrefix + nameof(ItemOverrides.Description)).HasNullableJsonListConversion();
            o.Property(x => x.Category).HasColumnName(ColumnPrefix + nameof(ItemOverrides.Category)).HasConversion<string>().HasMaxLength(32);
            o.Property(x => x.DamageDice).HasColumnName(ColumnPrefix + nameof(ItemOverrides.DamageDice)).HasMaxLength(ItemLimits.DiceMaxLength);
            o.Property(x => x.DamageType).HasColumnName(ColumnPrefix + nameof(ItemOverrides.DamageType)).HasMaxLength(ItemLimits.DamageTypeMaxLength);
            o.Property(x => x.VersatileDice).HasColumnName(ColumnPrefix + nameof(ItemOverrides.VersatileDice)).HasMaxLength(ItemLimits.DiceMaxLength);
            o.Property(x => x.Properties).HasColumnName(ColumnPrefix + nameof(ItemOverrides.Properties)).HasNullableJsonListConversion();
            o.Property(x => x.RangeNormal).HasColumnName(ColumnPrefix + nameof(ItemOverrides.RangeNormal));
            o.Property(x => x.RangeLong).HasColumnName(ColumnPrefix + nameof(ItemOverrides.RangeLong));
            o.Property(x => x.ArmorClassBase).HasColumnName(ColumnPrefix + nameof(ItemOverrides.ArmorClassBase));
            o.Property(x => x.AddDexModifier).HasColumnName(ColumnPrefix + nameof(ItemOverrides.AddDexModifier));
            o.Property(x => x.MaxDexBonus).HasColumnName(ColumnPrefix + nameof(ItemOverrides.MaxDexBonus));
            o.Property(x => x.StrengthMinimum).HasColumnName(ColumnPrefix + nameof(ItemOverrides.StrengthMinimum));
            o.Property(x => x.StealthDisadvantage).HasColumnName(ColumnPrefix + nameof(ItemOverrides.StealthDisadvantage));
            o.Property(x => x.WeightLb).HasColumnName(ColumnPrefix + nameof(ItemOverrides.WeightLb)).HasPrecision(10, 2);
            o.Property(x => x.Rarity).HasColumnName(ColumnPrefix + nameof(ItemOverrides.Rarity)).HasConversion<string>().HasMaxLength(16);
            o.Property(x => x.RequiresAttunement).HasColumnName(ColumnPrefix + nameof(ItemOverrides.RequiresAttunement));
            o.Property(x => x.AttackBonus).HasColumnName(ColumnPrefix + nameof(ItemOverrides.AttackBonus));
            o.Property(x => x.DamageBonus).HasColumnName(ColumnPrefix + nameof(ItemOverrides.DamageBonus));
            o.Property(x => x.Effects).HasColumnName(ColumnPrefix + nameof(ItemOverrides.Effects)).HasNullableJsonListConversion();
            o.Property(x => x.Modifiers).HasColumnName(ColumnPrefix + nameof(ItemOverrides.Modifiers)).HasNullableJsonListConversion();
            o.Ignore(x => x.IsEmpty);
        });
        builder.Navigation("Overrides").IsRequired();
    }
}
