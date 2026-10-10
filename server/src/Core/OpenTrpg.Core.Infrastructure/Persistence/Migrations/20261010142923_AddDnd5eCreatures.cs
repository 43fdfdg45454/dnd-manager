using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenTrpg.Core.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDnd5eCreatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Dnd5eCreatures",
                columns: table => new
                {
                    Index = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Subtype = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Size = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ChallengeRating = table.Column<double>(type: "double precision", nullable: false),
                    DataJson = table.Column<string>(type: "text", nullable: false),
                    Source = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Dnd5eCreatures", x => x.Index);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Dnd5eCreatures_Name",
                table: "Dnd5eCreatures",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Dnd5eCreatures_Source",
                table: "Dnd5eCreatures",
                column: "Source");

            migrationBuilder.CreateIndex(
                name: "IX_Dnd5eCreatures_Type",
                table: "Dnd5eCreatures",
                column: "Type");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Dnd5eCreatures");
        }
    }
}
