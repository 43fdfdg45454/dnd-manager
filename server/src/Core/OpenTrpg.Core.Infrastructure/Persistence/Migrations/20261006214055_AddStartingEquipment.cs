using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenTrpg.Core.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStartingEquipment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "StartingEquipmentJson",
                table: "CatalogClasses",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StartingEquipmentJson",
                table: "CatalogBackgrounds",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CatalogEquipmentCategories",
                columns: table => new
                {
                    Index = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ItemIndexes = table.Column<string>(type: "text", nullable: false),
                    Source = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CatalogEquipmentCategories", x => x.Index);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CatalogEquipmentCategories_Source",
                table: "CatalogEquipmentCategories",
                column: "Source");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CatalogEquipmentCategories");

            migrationBuilder.DropColumn(
                name: "StartingEquipmentJson",
                table: "CatalogClasses");

            migrationBuilder.DropColumn(
                name: "StartingEquipmentJson",
                table: "CatalogBackgrounds");
        }
    }
}
