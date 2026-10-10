using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenTrpg.Core.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCharacterHeightAndWeight : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "HeightInches",
                table: "Characters",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WeightPounds",
                table: "Characters",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HeightWeightJson",
                table: "CatalogSubraces",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HeightWeightJson",
                table: "CatalogRaces",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HeightWeightJson",
                table: "CatalogRaceExtensions",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HeightInches",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "WeightPounds",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "HeightWeightJson",
                table: "CatalogSubraces");

            migrationBuilder.DropColumn(
                name: "HeightWeightJson",
                table: "CatalogRaces");

            migrationBuilder.DropColumn(
                name: "HeightWeightJson",
                table: "CatalogRaceExtensions");
        }
    }
}
