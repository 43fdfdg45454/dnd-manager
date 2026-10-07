using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dnd.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCharacterPersonality : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BackgroundDetail",
                table: "Characters",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Bonds",
                table: "Characters",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Flaws",
                table: "Characters",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Ideals",
                table: "Characters",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PersonalityTraits",
                table: "Characters",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "OptionalTablesJson",
                table: "CatalogBackgrounds",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PersonalityJson",
                table: "CatalogBackgrounds",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CatalogRollTables",
                columns: table => new
                {
                    Source = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Dice = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    ClassIndex = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    SubclassIndex = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    EntriesJson = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CatalogRollTables", x => new { x.Source, x.Key });
                });

            migrationBuilder.CreateIndex(
                name: "IX_CatalogRollTables_SubclassIndex",
                table: "CatalogRollTables",
                column: "SubclassIndex");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CatalogRollTables");

            migrationBuilder.DropColumn(
                name: "BackgroundDetail",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "Bonds",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "Flaws",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "Ideals",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "PersonalityTraits",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "OptionalTablesJson",
                table: "CatalogBackgrounds");

            migrationBuilder.DropColumn(
                name: "PersonalityJson",
                table: "CatalogBackgrounds");
        }
    }
}
