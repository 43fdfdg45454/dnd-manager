using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenTrpg.Core.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRaceGrantsAndExtensions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GrantsJson",
                table: "CatalogSubraces",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Speed",
                table: "CatalogSubraces",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GrantsJson",
                table: "CatalogRaces",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CatalogRaceExtensions",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(161)", maxLength: 161, nullable: false),
                    RaceIndex = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    TraitIndexes = table.Column<string>(type: "text", nullable: false),
                    GrantsJson = table.Column<string>(type: "text", nullable: true),
                    Source = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CatalogRaceExtensions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CatalogRaceExtensions_CatalogRaces_RaceIndex",
                        column: x => x.RaceIndex,
                        principalTable: "CatalogRaces",
                        principalColumn: "Index",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CatalogRaceExtensions_RaceIndex",
                table: "CatalogRaceExtensions",
                column: "RaceIndex");

            migrationBuilder.CreateIndex(
                name: "IX_CatalogRaceExtensions_Source",
                table: "CatalogRaceExtensions",
                column: "Source");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CatalogRaceExtensions");

            migrationBuilder.DropColumn(
                name: "GrantsJson",
                table: "CatalogSubraces");

            migrationBuilder.DropColumn(
                name: "Speed",
                table: "CatalogSubraces");

            migrationBuilder.DropColumn(
                name: "GrantsJson",
                table: "CatalogRaces");
        }
    }
}
