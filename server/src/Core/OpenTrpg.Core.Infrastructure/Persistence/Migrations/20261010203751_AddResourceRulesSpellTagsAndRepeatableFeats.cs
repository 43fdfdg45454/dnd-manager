using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenTrpg.Core.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddResourceRulesSpellTagsAndRepeatableFeats : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Field",
                table: "CharacterOverrides",
                type: "character varying(132)",
                maxLength: 132,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(116)",
                oldMaxLength: 116);

            migrationBuilder.AddColumn<string>(
                name: "ResourceJson",
                table: "CatalogTraits",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Repeatable",
                table: "CatalogOptions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "CatalogSpellTags",
                columns: table => new
                {
                    Tag = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SpellIndex = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Source = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CatalogSpellTags", x => new { x.Source, x.Tag, x.SpellIndex });
                });

            migrationBuilder.CreateIndex(
                name: "IX_CatalogSpellTags_SpellIndex",
                table: "CatalogSpellTags",
                column: "SpellIndex");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CatalogSpellTags");

            migrationBuilder.DropColumn(
                name: "ResourceJson",
                table: "CatalogTraits");

            migrationBuilder.DropColumn(
                name: "Repeatable",
                table: "CatalogOptions");

            migrationBuilder.AlterColumn<string>(
                name: "Field",
                table: "CharacterOverrides",
                type: "character varying(116)",
                maxLength: 116,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(132)",
                oldMaxLength: 132);
        }
    }
}
