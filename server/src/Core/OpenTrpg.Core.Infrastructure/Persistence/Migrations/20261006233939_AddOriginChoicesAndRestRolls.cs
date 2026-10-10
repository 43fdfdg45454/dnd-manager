using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenTrpg.Core.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOriginChoicesAndRestRolls : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RollCount",
                table: "CharacterResources",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RollDie",
                table: "CharacterResources",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RollRest",
                table: "CharacterResources",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RollsJson",
                table: "CharacterResources",
                type: "text",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<bool>(
                name: "RollsPending",
                table: "CharacterResources",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AlterColumn<string>(
                name: "ClassIndex",
                table: "CharacterChoices",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AddColumn<string>(
                name: "ChoicesJson",
                table: "CatalogSubraces",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Resistances",
                table: "CatalogSubraces",
                type: "text",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "ChoicesJson",
                table: "CatalogRaces",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Resistances",
                table: "CatalogRaces",
                type: "text",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "ChoicesJson",
                table: "CatalogBackgrounds",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RollCount",
                table: "CharacterResources");

            migrationBuilder.DropColumn(
                name: "RollDie",
                table: "CharacterResources");

            migrationBuilder.DropColumn(
                name: "RollRest",
                table: "CharacterResources");

            migrationBuilder.DropColumn(
                name: "RollsJson",
                table: "CharacterResources");

            migrationBuilder.DropColumn(
                name: "RollsPending",
                table: "CharacterResources");

            migrationBuilder.DropColumn(
                name: "ChoicesJson",
                table: "CatalogSubraces");

            migrationBuilder.DropColumn(
                name: "Resistances",
                table: "CatalogSubraces");

            migrationBuilder.DropColumn(
                name: "ChoicesJson",
                table: "CatalogRaces");

            migrationBuilder.DropColumn(
                name: "Resistances",
                table: "CatalogRaces");

            migrationBuilder.DropColumn(
                name: "ChoicesJson",
                table: "CatalogBackgrounds");

            migrationBuilder.AlterColumn<string>(
                name: "ClassIndex",
                table: "CharacterChoices",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);
        }
    }
}
