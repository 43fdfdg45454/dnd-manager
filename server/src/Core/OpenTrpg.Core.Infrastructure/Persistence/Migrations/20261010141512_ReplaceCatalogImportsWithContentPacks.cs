using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenTrpg.Core.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceCatalogImportsWithContentPacks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ContentPacks",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    SystemId = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Version = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    FormatVersion = table.Column<int>(type: "integer", nullable: false),
                    IsBase = table.Column<bool>(type: "boolean", nullable: false),
                    ImportedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CountsJson = table.Column<string>(type: "text", nullable: false),
                    Requires = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContentPacks", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ContentPacks_SystemId",
                table: "ContentPacks",
                column: "SystemId");

            // The registered packs move over; the SRD keeps a truncated version so that the seeder loads it again with
            // the new tables (creatures, rules, vocabularies) and writes its base row.
            migrationBuilder.Sql("""
                INSERT INTO "ContentPacks" ("Id", "SystemId", "Name", "Version", "FormatVersion", "IsBase", "ImportedAt", "CountsJson", "Requires")
                SELECT substring("Ruleset" from 6), 'dnd5e', COALESCE("Name", substring("Ruleset" from 6)), left("DatasetVersion", 40), 2, FALSE,
                       "ImportedAt", "CountsJson", '[]'
                FROM "CatalogImports" WHERE "Ruleset" LIKE 'pack:%';
                INSERT INTO "ContentPacks" ("Id", "SystemId", "Name", "Version", "FormatVersion", "IsBase", "ImportedAt", "CountsJson", "Requires")
                SELECT 'srd', 'dnd5e', 'SRD 5.1', left("DatasetVersion", 40), 0, TRUE, "ImportedAt", "CountsJson", '[]'
                FROM "CatalogImports" WHERE "Ruleset" = 'srd-5.1';
                """);

            migrationBuilder.DropTable(
                name: "CatalogImports");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ContentPacks");

            migrationBuilder.CreateTable(
                name: "CatalogImports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CountsJson = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DatasetVersion = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ImportedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Ruleset = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CatalogImports", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CatalogImports_Ruleset_DatasetVersion",
                table: "CatalogImports",
                columns: new[] { "Ruleset", "DatasetVersion" },
                unique: true);
        }
    }
}
