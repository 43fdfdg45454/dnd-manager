using Dnd.Application.Abstractions.Persistence;
using Dnd.Domain.Campaigns;
using Dnd.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace Dnd.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), IUnitOfWork
{
    public DbSet<User> Users => Set<User>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<PasswordToken> PasswordTokens => Set<PasswordToken>();

    public DbSet<Campaign> Campaigns => Set<Campaign>();

    public DbSet<CampaignMember> CampaignMembers => Set<CampaignMember>();

    public DbSet<OwnershipTransfer> OwnershipTransfers => Set<OwnershipTransfer>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
