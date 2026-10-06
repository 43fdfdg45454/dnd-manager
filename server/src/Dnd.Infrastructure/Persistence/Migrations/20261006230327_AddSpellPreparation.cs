using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dnd.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSpellPreparation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "SpellPreparationPending",
                table: "Characters",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "SpellPreparationReason",
                table: "Characters",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "CatalogSpells",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Utility");

            // Existing spells (content packs; the SRD ones are re-imported with their curated category): the same
            // derivation as on import, without healing data (damage → Damage, saving throw → Control).
            migrationBuilder.Sql(
                """
                UPDATE "CatalogSpells"
                SET "Category" = CASE
                    WHEN "DamageJson" IS NOT NULL THEN 'Damage'
                    WHEN "DcAbility" IS NOT NULL THEN 'Control'
                    ELSE 'Utility'
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SpellPreparationPending",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "SpellPreparationReason",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "Category",
                table: "CatalogSpells");
        }
    }
}
