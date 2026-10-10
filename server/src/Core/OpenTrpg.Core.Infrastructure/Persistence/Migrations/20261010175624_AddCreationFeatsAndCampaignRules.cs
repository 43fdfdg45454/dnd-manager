using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenTrpg.Core.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCreationFeatsAndCampaignRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "CreationOnly",
                table: "CatalogOptions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "Starter",
                table: "CatalogOptions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "Dnd5eCampaignRules",
                columns: table => new
                {
                    CampaignId = table.Column<Guid>(type: "uuid", nullable: false),
                    StarterFeatsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Dnd5eCampaignRules", x => x.CampaignId);
                    table.ForeignKey(
                        name: "FK_Dnd5eCampaignRules_Campaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "Campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Dnd5eCampaignRules");

            migrationBuilder.DropColumn(
                name: "CreationOnly",
                table: "CatalogOptions");

            migrationBuilder.DropColumn(
                name: "Starter",
                table: "CatalogOptions");
        }
    }
}
