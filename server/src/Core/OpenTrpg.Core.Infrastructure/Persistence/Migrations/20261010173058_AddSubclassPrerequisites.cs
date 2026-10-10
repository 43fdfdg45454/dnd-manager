using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenTrpg.Core.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSubclassPrerequisites : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PrerequisitesJson",
                table: "CatalogSubclasses",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PrerequisitesText",
                table: "CatalogSubclasses",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PrerequisitesJson",
                table: "CatalogSubclasses");

            migrationBuilder.DropColumn(
                name: "PrerequisitesText",
                table: "CatalogSubclasses");
        }
    }
}
