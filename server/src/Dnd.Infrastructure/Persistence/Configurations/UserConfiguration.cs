using Dnd.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dnd.Infrastructure.Persistence.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.Email).HasMaxLength(User.EmailMaxLength).IsRequired();
        builder.HasIndex(x => x.Email).IsUnique();

        builder.Property(x => x.DisplayName).HasMaxLength(User.DisplayNameMaxLength).IsRequired();
        builder.Property(x => x.PasswordHash).HasMaxLength(512);
        builder.Property(x => x.Role).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(x => x.IsActive).IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.LastLoginAt);

        builder.Ignore(x => x.HasPassword);
    }
}
