using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenTrpg.Core.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ItemDescriptionOverrides : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CatalogItemDescriptionOverrides",
                columns: table => new
                {
                    Source = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    ItemIndex = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    OriginalDescription = table.Column<string>(type: "text", nullable: false),
                    ImportedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CatalogItemDescriptionOverrides", x => new { x.Source, x.ItemIndex });
                });

            migrationBuilder.CreateIndex(
                name: "IX_CatalogItemDescriptionOverrides_ItemIndex",
                table: "CatalogItemDescriptionOverrides",
                column: "ItemIndex");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CatalogItemDescriptionOverrides");
        }
    }
}
