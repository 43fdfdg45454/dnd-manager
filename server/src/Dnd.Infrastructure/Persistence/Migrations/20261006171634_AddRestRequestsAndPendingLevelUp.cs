using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dnd.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRestRequestsAndPendingLevelUp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LevelGrantedAt",
                table: "Characters",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LevelGrantedByUserId",
                table: "Characters",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PendingLevelUpTo",
                table: "Characters",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "RestRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CampaignId = table.Column<Guid>(type: "uuid", nullable: false),
                    CharacterId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    HitDiceJson = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    RequestedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ResolvedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ResolvedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Comment = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RestRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RestRequests_Campaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "Campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RestRequests_Characters_CharacterId",
                        column: x => x.CharacterId,
                        principalTable: "Characters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RestRequests_Users_RequestedByUserId",
                        column: x => x.RequestedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RestRequests_Users_ResolvedByUserId",
                        column: x => x.ResolvedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Characters_LevelGrantedByUserId",
                table: "Characters",
                column: "LevelGrantedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_RestRequests_CampaignId_Status",
                table: "RestRequests",
                columns: new[] { "CampaignId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_RestRequests_CharacterId_Pending",
                table: "RestRequests",
                column: "CharacterId",
                unique: true,
                filter: "\"Status\" = 'Pending'");

            migrationBuilder.CreateIndex(
                name: "IX_RestRequests_CharacterId_Status",
                table: "RestRequests",
                columns: new[] { "CharacterId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_RestRequests_RequestedByUserId",
                table: "RestRequests",
                column: "RequestedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_RestRequests_ResolvedByUserId",
                table: "RestRequests",
                column: "ResolvedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Characters_Users_LevelGrantedByUserId",
                table: "Characters",
                column: "LevelGrantedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Characters_Users_LevelGrantedByUserId",
                table: "Characters");

            migrationBuilder.DropTable(
                name: "RestRequests");

            migrationBuilder.DropIndex(
                name: "IX_Characters_LevelGrantedByUserId",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "LevelGrantedAt",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "LevelGrantedByUserId",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "PendingLevelUpTo",
                table: "Characters");
        }
    }
}
