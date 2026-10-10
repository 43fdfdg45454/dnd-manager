using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenTrpg.Core.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDnd5eRulesReferenceEntriesAndSources : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SystemDataJson",
                table: "ItemTemplates",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "CatalogConditions",
                type: "character varying(60)",
                maxLength: 60,
                nullable: false,
                defaultValue: "srd");

            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "CatalogClassLevels",
                type: "character varying(60)",
                maxLength: 60,
                nullable: false,
                defaultValue: "srd");

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "CatalogClasses",
                type: "text",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "MulticlassJson",
                table: "CatalogClasses",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResourcesJson",
                table: "CatalogClasses",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "CatalogClasses",
                type: "character varying(60)",
                maxLength: 60,
                nullable: false,
                defaultValue: "srd");

            migrationBuilder.AddColumn<string>(
                name: "SpellListJson",
                table: "CatalogClasses",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SpellcastingJson",
                table: "CatalogClasses",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SubclassLevel",
                table: "CatalogClasses",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "Dnd5eReferenceEntries",
                columns: table => new
                {
                    Kind = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Index = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DescriptionJson = table.Column<string>(type: "text", nullable: false),
                    Source = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Dnd5eReferenceEntries", x => new { x.Kind, x.Index });
                });

            migrationBuilder.CreateTable(
                name: "Dnd5eRules",
                columns: table => new
                {
                    Index = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Category = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Body = table.Column<string>(type: "text", nullable: false),
                    Tags = table.Column<string>(type: "text", nullable: false),
                    Source = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Dnd5eRules", x => x.Index);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CatalogConditions_Source",
                table: "CatalogConditions",
                column: "Source");

            migrationBuilder.CreateIndex(
                name: "IX_CatalogClassLevels_Source",
                table: "CatalogClassLevels",
                column: "Source");

            migrationBuilder.CreateIndex(
                name: "IX_CatalogClasses_Source",
                table: "CatalogClasses",
                column: "Source");

            migrationBuilder.CreateIndex(
                name: "IX_Dnd5eReferenceEntries_Source",
                table: "Dnd5eReferenceEntries",
                column: "Source");

            migrationBuilder.CreateIndex(
                name: "IX_Dnd5eRules_Source",
                table: "Dnd5eRules",
                column: "Source");

            migrationBuilder.CreateIndex(
                name: "IX_Dnd5eRules_Title",
                table: "Dnd5eRules",
                column: "Title");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Dnd5eReferenceEntries");

            migrationBuilder.DropTable(
                name: "Dnd5eRules");

            migrationBuilder.DropIndex(
                name: "IX_CatalogConditions_Source",
                table: "CatalogConditions");

            migrationBuilder.DropIndex(
                name: "IX_CatalogClassLevels_Source",
                table: "CatalogClassLevels");

            migrationBuilder.DropIndex(
                name: "IX_CatalogClasses_Source",
                table: "CatalogClasses");

            migrationBuilder.DropColumn(
                name: "SystemDataJson",
                table: "ItemTemplates");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "CatalogConditions");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "CatalogClassLevels");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "CatalogClasses");

            migrationBuilder.DropColumn(
                name: "MulticlassJson",
                table: "CatalogClasses");

            migrationBuilder.DropColumn(
                name: "ResourcesJson",
                table: "CatalogClasses");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "CatalogClasses");

            migrationBuilder.DropColumn(
                name: "SpellListJson",
                table: "CatalogClasses");

            migrationBuilder.DropColumn(
                name: "SpellcastingJson",
                table: "CatalogClasses");

            migrationBuilder.DropColumn(
                name: "SubclassLevel",
                table: "CatalogClasses");
        }
    }
}
