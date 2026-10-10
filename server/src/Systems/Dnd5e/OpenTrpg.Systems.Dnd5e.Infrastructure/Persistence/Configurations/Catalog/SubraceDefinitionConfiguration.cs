using OpenTrpg.Core.Domain.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenTrpg.Core.Infrastructure;
using OpenTrpg.Core.Infrastructure.Persistence;
using OpenTrpg.Core.Infrastructure.Persistence.Configurations;
using OpenTrpg.Core.Infrastructure.Persistence.Configurations.Catalog;
using OpenTrpg.Systems.Dnd5e.Domain.Catalog;
using OpenTrpg.Systems.Dnd5e.Infrastructure.Persistence.Configurations.Catalog;

namespace OpenTrpg.Systems.Dnd5e.Infrastructure.Persistence.Configurations.Catalog;

internal sealed class SubraceDefinitionConfiguration : IEntityTypeConfiguration<SubraceDefinition>
{
    public void Configure(EntityTypeBuilder<SubraceDefinition> builder)
    {
        builder.ToTable("CatalogSubraces");
        builder.HasKey(x => x.Index);
        builder.Property(x => x.Source).HasMaxLength(CatalogSources.MaxLength).IsRequired();
        builder.HasIndex(x => x.Source);
        builder.Property(x => x.Index).HasMaxLength(CatalogColumns.IndexMaxLength);
        builder.Property(x => x.Name).HasMaxLength(CatalogColumns.NameMaxLength).IsRequired();
        builder.HasIndex(x => x.Name);
        builder.Property(x => x.RaceIndex).HasMaxLength(CatalogColumns.IndexMaxLength).IsRequired();
        builder.HasIndex(x => x.RaceIndex);
        builder.HasOne<RaceDefinition>().WithMany().HasForeignKey(x => x.RaceIndex).OnDelete(DeleteBehavior.Cascade);

        builder.Property(x => x.Description).IsRequired();
        builder.Property(x => x.AbilityBonusesJson).IsRequired();
        builder.Property(x => x.TraitIndexes).HasJsonListConversion();
        builder.Property(x => x.Resistances).HasJsonListConversion();
        builder.Ignore(x => x.Choices);
        builder.Ignore(x => x.Grants);
        builder.Ignore(x => x.HeightWeight);
    }
}
