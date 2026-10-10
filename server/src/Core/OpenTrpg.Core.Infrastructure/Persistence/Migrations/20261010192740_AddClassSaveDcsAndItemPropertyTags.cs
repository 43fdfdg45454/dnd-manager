using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenTrpg.Core.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddClassSaveDcsAndItemPropertyTags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PropertyTags",
                table: "ItemTemplates",
                type: "text",
                nullable: false,
                defaultValue: "");

            // Existing items: the same tags the domain writes (ItemTemplate.TagsOf), so the catalog filters by property.
            migrationBuilder.Sql(
                """
                UPDATE "ItemTemplates"
                SET "PropertyTags" = COALESCE(
                    '|' || (SELECT string_agg(DISTINCT lower(trim(p)), '|') FROM json_array_elements_text("Properties"::json) AS p WHERE trim(p) <> '') || '|',
                    '')
                WHERE "Properties" <> '[]';
                """);

            migrationBuilder.AddColumn<string>(
                name: "SaveDcsJson",
                table: "CatalogClasses",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PropertyTags",
                table: "ItemTemplates");

            migrationBuilder.DropColumn(
                name: "SaveDcsJson",
                table: "CatalogClasses");
        }
    }
}
