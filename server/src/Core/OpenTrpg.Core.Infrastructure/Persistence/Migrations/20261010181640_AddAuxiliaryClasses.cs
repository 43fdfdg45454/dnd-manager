using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenTrpg.Core.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAuxiliaryClasses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "CountsAsCasterLevel",
                table: "CatalogClasses",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsAuxiliary",
                table: "CatalogClasses",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "PrerequisitesJson",
                table: "CatalogClasses",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PrerequisitesText",
                table: "CatalogClasses",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CountsAsCasterLevel",
                table: "CatalogClasses");

            migrationBuilder.DropColumn(
                name: "IsAuxiliary",
                table: "CatalogClasses");

            migrationBuilder.DropColumn(
                name: "PrerequisitesJson",
                table: "CatalogClasses");

            migrationBuilder.DropColumn(
                name: "PrerequisitesText",
                table: "CatalogClasses");
        }
    }
}
