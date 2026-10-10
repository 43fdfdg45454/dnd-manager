using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenTrpg.Core.Domain.Campaigns;
using OpenTrpg.Systems.Dnd5e.Domain.Rules;

namespace OpenTrpg.Systems.Dnd5e.Infrastructure.Persistence.Configurations.Rules;

internal sealed class CampaignRulesConfiguration : IEntityTypeConfiguration<Dnd5eCampaignRules>
{
    public void Configure(EntityTypeBuilder<Dnd5eCampaignRules> builder)
    {
        builder.ToTable("Dnd5eCampaignRules");
        builder.HasKey(x => x.CampaignId);
        builder.Property(x => x.CampaignId).ValueGeneratedNever();
        builder.Property(x => x.UpdatedAt).IsRequired();
        builder.HasOne<Campaign>().WithOne().HasForeignKey<Dnd5eCampaignRules>(x => x.CampaignId).OnDelete(DeleteBehavior.Cascade);
    }
}
