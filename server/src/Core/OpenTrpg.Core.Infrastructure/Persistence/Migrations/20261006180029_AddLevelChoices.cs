using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenTrpg.Core.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLevelChoices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GrantsJson",
                table: "CatalogSubclassLevels",
                type: "text",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "DatasetVersion",
                table: "CatalogImports",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.CreateTable(
                name: "CatalogLevelChoiceRules",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(308)", maxLength: 308, nullable: false),
                    ClassIndex = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SubclassIndex = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    Key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    SetId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Choose = table.Column<int>(type: "integer", nullable: false),
                    FromJson = table.Column<string>(type: "text", nullable: true),
                    FilterJson = table.Column<string>(type: "text", nullable: true),
                    Replaces = table.Column<bool>(type: "boolean", nullable: false),
                    Cumulative = table.Column<bool>(type: "boolean", nullable: false),
                    Note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Source = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CatalogLevelChoiceRules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CatalogOptions",
                columns: table => new
                {
                    Index = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SetId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    PrerequisitesText = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    PrerequisitesJson = table.Column<string>(type: "text", nullable: true),
                    ModifiersJson = table.Column<string>(type: "text", nullable: false),
                    AbilityIncreaseJson = table.Column<string>(type: "text", nullable: true),
                    GrantsJson = table.Column<string>(type: "text", nullable: true),
                    ResourceJson = table.Column<string>(type: "text", nullable: true),
                    Source = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CatalogOptions", x => x.Index);
                });

            migrationBuilder.CreateTable(
                name: "CatalogOptionSets",
                columns: table => new
                {
                    SetId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Source = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CatalogOptionSets", x => x.SetId);
                });

            migrationBuilder.CreateTable(
                name: "CharacterChoices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CharacterId = table.Column<Guid>(type: "uuid", nullable: false),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    ClassIndex = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SelectedJson = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CharacterChoices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CharacterChoices_Characters_CharacterId",
                        column: x => x.CharacterId,
                        principalTable: "Characters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CatalogLevelChoiceRules_ClassIndex_Level",
                table: "CatalogLevelChoiceRules",
                columns: new[] { "ClassIndex", "Level" });

            migrationBuilder.CreateIndex(
                name: "IX_CatalogLevelChoiceRules_Source",
                table: "CatalogLevelChoiceRules",
                column: "Source");

            migrationBuilder.CreateIndex(
                name: "IX_CatalogOptions_SetId",
                table: "CatalogOptions",
                column: "SetId");

            migrationBuilder.CreateIndex(
                name: "IX_CatalogOptions_Source",
                table: "CatalogOptions",
                column: "Source");

            migrationBuilder.CreateIndex(
                name: "IX_CatalogOptionSets_Source",
                table: "CatalogOptionSets",
                column: "Source");

            migrationBuilder.CreateIndex(
                name: "IX_CharacterChoices_CharacterId_ClassIndex_Key",
                table: "CharacterChoices",
                columns: new[] { "CharacterId", "ClassIndex", "Key" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CatalogLevelChoiceRules");

            migrationBuilder.DropTable(
                name: "CatalogOptions");

            migrationBuilder.DropTable(
                name: "CatalogOptionSets");

            migrationBuilder.DropTable(
                name: "CharacterChoices");

            migrationBuilder.DropColumn(
                name: "GrantsJson",
                table: "CatalogSubclassLevels");

            migrationBuilder.AlterColumn<string>(
                name: "DatasetVersion",
                table: "CatalogImports",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);
        }
    }
}
