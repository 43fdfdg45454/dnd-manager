using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenTrpg.Core.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOptionChoices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ChoicesJson",
                table: "CatalogOptions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LevelChoicesJson",
                table: "CatalogOptions",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ChoicesJson",
                table: "CatalogOptions");

            migrationBuilder.DropColumn(
                name: "LevelChoicesJson",
                table: "CatalogOptions");
        }
    }
}
