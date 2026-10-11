using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenTrpg.Core.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AllowSeveralCompanionsPerCharacter : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CharacterCompanions_CharacterId",
                table: "CharacterCompanions");

            migrationBuilder.CreateIndex(
                name: "IX_CharacterCompanions_CharacterId",
                table: "CharacterCompanions",
                column: "CharacterId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CharacterCompanions_CharacterId",
                table: "CharacterCompanions");

            migrationBuilder.CreateIndex(
                name: "IX_CharacterCompanions_CharacterId",
                table: "CharacterCompanions",
                column: "CharacterId",
                unique: true);
        }
    }
}
