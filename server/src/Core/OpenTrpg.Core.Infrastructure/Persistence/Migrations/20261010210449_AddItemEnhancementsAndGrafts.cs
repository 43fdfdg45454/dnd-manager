using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenTrpg.Core.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddItemEnhancementsAndGrafts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ItemEnhancementsJson",
                table: "Dnd5eCharacters",
                type: "text",
                nullable: false,
                defaultValue: "{}");

            migrationBuilder.AddColumn<string>(
                name: "ItemEffectsJson",
                table: "CatalogOptions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ItemEnhancementJson",
                table: "CatalogFeatures",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ItemEnhancementsJson",
                table: "Dnd5eCharacters");

            migrationBuilder.DropColumn(
                name: "ItemEffectsJson",
                table: "CatalogOptions");

            migrationBuilder.DropColumn(
                name: "ItemEnhancementJson",
                table: "CatalogFeatures");
        }
    }
}
