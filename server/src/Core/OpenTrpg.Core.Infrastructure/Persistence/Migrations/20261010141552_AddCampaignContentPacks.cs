using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenTrpg.Core.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCampaignContentPacks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CampaignContentPacks",
                columns: table => new
                {
                    CampaignId = table.Column<Guid>(type: "uuid", nullable: false),
                    PackId = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    EnabledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EnabledByUserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CampaignContentPacks", x => new { x.CampaignId, x.PackId });
                    table.ForeignKey(
                        name: "FK_CampaignContentPacks_Campaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "Campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CampaignContentPacks_ContentPacks_PackId",
                        column: x => x.PackId,
                        principalTable: "ContentPacks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CampaignContentPacks_PackId",
                table: "CampaignContentPacks",
                column: "PackId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CampaignContentPacks");
        }
    }
}
