using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dnd.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTrinketTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CatalogTrinkets",
                columns: table => new
                {
                    Source = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Roll = table.Column<int>(type: "integer", nullable: false),
                    ItemIndex = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CatalogTrinkets", x => new { x.Source, x.Roll });
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CatalogTrinkets");
        }
    }
}
