using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dnd.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCatalogSource : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "ItemTemplates",
                type: "character varying(60)",
                maxLength: 60,
                nullable: false,
                defaultValue: "srd");

            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "CatalogTraits",
                type: "character varying(60)",
                maxLength: 60,
                nullable: false,
                defaultValue: "srd");

            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "CatalogSubraces",
                type: "character varying(60)",
                maxLength: 60,
                nullable: false,
                defaultValue: "srd");

            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "CatalogSubclassLevels",
                type: "character varying(60)",
                maxLength: 60,
                nullable: false,
                defaultValue: "srd");

            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "CatalogSubclasses",
                type: "character varying(60)",
                maxLength: 60,
                nullable: false,
                defaultValue: "srd");

            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "CatalogSpells",
                type: "character varying(60)",
                maxLength: 60,
                nullable: false,
                defaultValue: "srd");

            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "CatalogRaces",
                type: "character varying(60)",
                maxLength: 60,
                nullable: false,
                defaultValue: "srd");

            migrationBuilder.AlterColumn<string>(
                name: "Ruleset",
                table: "CatalogImports",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(32)",
                oldMaxLength: 32);

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "CatalogImports",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "CatalogFeatures",
                type: "character varying(60)",
                maxLength: 60,
                nullable: false,
                defaultValue: "srd");

            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "CatalogBackgrounds",
                type: "character varying(60)",
                maxLength: 60,
                nullable: false,
                defaultValue: "srd");

            // Existing definitions come from the SRD; campaign items are homebrew.
            migrationBuilder.Sql("UPDATE \"ItemTemplates\" SET \"Source\" = 'homebrew' WHERE \"CampaignId\" IS NOT NULL;");

            migrationBuilder.CreateIndex(
                name: "IX_ItemTemplates_Source",
                table: "ItemTemplates",
                column: "Source");

            migrationBuilder.CreateIndex(
                name: "IX_CatalogTraits_Source",
                table: "CatalogTraits",
                column: "Source");

            migrationBuilder.CreateIndex(
                name: "IX_CatalogSubraces_Source",
                table: "CatalogSubraces",
                column: "Source");

            migrationBuilder.CreateIndex(
                name: "IX_CatalogSubclassLevels_Source",
                table: "CatalogSubclassLevels",
                column: "Source");

            migrationBuilder.CreateIndex(
                name: "IX_CatalogSubclasses_Source",
                table: "CatalogSubclasses",
                column: "Source");

            migrationBuilder.CreateIndex(
                name: "IX_CatalogSpells_Source",
                table: "CatalogSpells",
                column: "Source");

            migrationBuilder.CreateIndex(
                name: "IX_CatalogRaces_Source",
                table: "CatalogRaces",
                column: "Source");

            migrationBuilder.CreateIndex(
                name: "IX_CatalogFeatures_Source",
                table: "CatalogFeatures",
                column: "Source");

            migrationBuilder.CreateIndex(
                name: "IX_CatalogBackgrounds_Source",
                table: "CatalogBackgrounds",
                column: "Source");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ItemTemplates_Source",
                table: "ItemTemplates");

            migrationBuilder.DropIndex(
                name: "IX_CatalogTraits_Source",
                table: "CatalogTraits");

            migrationBuilder.DropIndex(
                name: "IX_CatalogSubraces_Source",
                table: "CatalogSubraces");

            migrationBuilder.DropIndex(
                name: "IX_CatalogSubclassLevels_Source",
                table: "CatalogSubclassLevels");

            migrationBuilder.DropIndex(
                name: "IX_CatalogSubclasses_Source",
                table: "CatalogSubclasses");

            migrationBuilder.DropIndex(
                name: "IX_CatalogSpells_Source",
                table: "CatalogSpells");

            migrationBuilder.DropIndex(
                name: "IX_CatalogRaces_Source",
                table: "CatalogRaces");

            migrationBuilder.DropIndex(
                name: "IX_CatalogFeatures_Source",
                table: "CatalogFeatures");

            migrationBuilder.DropIndex(
                name: "IX_CatalogBackgrounds_Source",
                table: "CatalogBackgrounds");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "ItemTemplates");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "CatalogTraits");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "CatalogSubraces");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "CatalogSubclassLevels");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "CatalogSubclasses");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "CatalogSpells");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "CatalogRaces");

            migrationBuilder.DropColumn(
                name: "Name",
                table: "CatalogImports");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "CatalogFeatures");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "CatalogBackgrounds");

            migrationBuilder.AlterColumn<string>(
                name: "Ruleset",
                table: "CatalogImports",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64);
        }
    }
}
