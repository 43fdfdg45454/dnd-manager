using OpenTrpg.Core.Domain.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace OpenTrpg.Core.Infrastructure.Persistence.Configurations.Catalog;

internal sealed class CatalogImportConfiguration : IEntityTypeConfiguration<CatalogImport>
{
    public void Configure(EntityTypeBuilder<CatalogImport> builder)
    {
        builder.ToTable("CatalogImports");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Ruleset).HasMaxLength(CatalogImport.RulesetMaxLength).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(CatalogColumns.NameMaxLength);
        builder.Property(x => x.DatasetVersion).HasMaxLength(200).IsRequired();
        builder.HasIndex(x => new { x.Ruleset, x.DatasetVersion }).IsUnique();
        builder.Property(x => x.ImportedAt).IsRequired();
        builder.Property(x => x.CountsJson).IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();
    }
}
