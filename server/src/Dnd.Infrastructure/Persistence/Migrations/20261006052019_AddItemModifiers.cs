using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dnd.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddItemModifiers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OverrideModifiers",
                table: "ShopItems",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Modifiers",
                table: "ItemTemplates",
                type: "text",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "OverrideModifiers",
                table: "CharacterItems",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OverrideModifiers",
                table: "ShopItems");

            migrationBuilder.DropColumn(
                name: "Modifiers",
                table: "ItemTemplates");

            migrationBuilder.DropColumn(
                name: "OverrideModifiers",
                table: "CharacterItems");
        }
    }
}
