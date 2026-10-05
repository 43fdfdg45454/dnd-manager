using Dnd.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dnd.Infrastructure.Persistence.Configurations;

internal sealed class InstanceSettingConfiguration : IEntityTypeConfiguration<InstanceSetting>
{
    public void Configure(EntityTypeBuilder<InstanceSetting> builder)
    {
        builder.ToTable("InstanceSettings");
        builder.HasKey(x => x.Key);
        builder.Property(x => x.Key).HasMaxLength(InstanceSetting.KeyMaxLength).ValueGeneratedNever();
        builder.Property(x => x.Value).HasMaxLength(InstanceSetting.ValueMaxLength).IsRequired();
        builder.Property(x => x.UpdatedAt).IsRequired();
    }
}
