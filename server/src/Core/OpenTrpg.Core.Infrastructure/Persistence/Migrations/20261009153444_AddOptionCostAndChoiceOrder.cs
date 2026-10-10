using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenTrpg.Core.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOptionCostAndChoiceOrder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CostJson",
                table: "CatalogOptions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "After",
                table: "CatalogLevelChoiceRules",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CostJson",
                table: "CatalogOptions");

            migrationBuilder.DropColumn(
                name: "After",
                table: "CatalogLevelChoiceRules");
        }
    }
}
