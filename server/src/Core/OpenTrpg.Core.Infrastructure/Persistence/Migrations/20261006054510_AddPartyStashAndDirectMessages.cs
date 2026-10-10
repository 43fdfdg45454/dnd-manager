using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenTrpg.Core.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPartyStashAndDirectMessages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "ShopId",
                table: "Transactions",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<Guid>(
                name: "CharacterId",
                table: "Transactions",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "ActorUserId",
                table: "Transactions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PlayersCanTakeFromStash",
                table: "Campaigns",
                type: "boolean",
                nullable: false,
                // Existing campaigns keep the domain default: players may take from the stash.
                defaultValue: true);

            migrationBuilder.AddColumn<long>(
                name: "StashCopperPieces",
                table: "Campaigns",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateTable(
                name: "DirectMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CampaignId = table.Column<Guid>(type: "uuid", nullable: false),
                    SenderUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecipientUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CharacterId = table.Column<Guid>(type: "uuid", nullable: false),
                    Body = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    SentAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ReadAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DirectMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DirectMessages_Campaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "Campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DirectMessages_Characters_CharacterId",
                        column: x => x.CharacterId,
                        principalTable: "Characters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DirectMessages_Users_RecipientUserId",
                        column: x => x.RecipientUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DirectMessages_Users_SenderUserId",
                        column: x => x.SenderUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PartyStashItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CampaignId = table.Column<Guid>(type: "uuid", nullable: false),
                    TemplateId = table.Column<Guid>(type: "uuid", nullable: true),
                    OverrideName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    OverrideDescription = table.Column<string>(type: "text", nullable: true),
                    OverrideCategory = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    OverrideDamageDice = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    OverrideDamageType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    OverrideVersatileDice = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    OverrideProperties = table.Column<string>(type: "text", nullable: true),
                    OverrideRangeNormal = table.Column<int>(type: "integer", nullable: true),
                    OverrideRangeLong = table.Column<int>(type: "integer", nullable: true),
                    OverrideArmorClassBase = table.Column<int>(type: "integer", nullable: true),
                    OverrideAddDexModifier = table.Column<bool>(type: "boolean", nullable: true),
                    OverrideMaxDexBonus = table.Column<int>(type: "integer", nullable: true),
                    OverrideStrengthMinimum = table.Column<int>(type: "integer", nullable: true),
                    OverrideStealthDisadvantage = table.Column<bool>(type: "boolean", nullable: true),
                    OverrideWeightLb = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    OverrideRarity = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    OverrideRequiresAttunement = table.Column<bool>(type: "boolean", nullable: true),
                    OverrideAttackBonus = table.Column<int>(type: "integer", nullable: true),
                    OverrideDamageBonus = table.Column<int>(type: "integer", nullable: true),
                    OverrideEffects = table.Column<string>(type: "text", nullable: true),
                    OverrideModifiers = table.Column<string>(type: "text", nullable: true),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    Charges = table.Column<int>(type: "integer", nullable: true),
                    ChargesMax = table.Column<int>(type: "integer", nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    AddedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AddedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PartyStashItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PartyStashItems_Campaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "Campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PartyStashItems_ItemTemplates_TemplateId",
                        column: x => x.TemplateId,
                        principalTable: "ItemTemplates",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PartyStashItems_Users_AddedByUserId",
                        column: x => x.AddedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_ActorUserId",
                table: "Transactions",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DirectMessages_CampaignId_RecipientUserId_ReadAt",
                table: "DirectMessages",
                columns: new[] { "CampaignId", "RecipientUserId", "ReadAt" });

            migrationBuilder.CreateIndex(
                name: "IX_DirectMessages_CampaignId_SenderUserId",
                table: "DirectMessages",
                columns: new[] { "CampaignId", "SenderUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_DirectMessages_CharacterId",
                table: "DirectMessages",
                column: "CharacterId");

            migrationBuilder.CreateIndex(
                name: "IX_DirectMessages_RecipientUserId",
                table: "DirectMessages",
                column: "RecipientUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DirectMessages_SenderUserId",
                table: "DirectMessages",
                column: "SenderUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PartyStashItems_AddedByUserId",
                table: "PartyStashItems",
                column: "AddedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PartyStashItems_CampaignId",
                table: "PartyStashItems",
                column: "CampaignId");

            migrationBuilder.CreateIndex(
                name: "IX_PartyStashItems_TemplateId",
                table: "PartyStashItems",
                column: "TemplateId");

            migrationBuilder.AddForeignKey(
                name: "FK_Transactions_Users_ActorUserId",
                table: "Transactions",
                column: "ActorUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Transactions_Users_ActorUserId",
                table: "Transactions");

            migrationBuilder.DropTable(
                name: "DirectMessages");

            migrationBuilder.DropTable(
                name: "PartyStashItems");

            migrationBuilder.DropIndex(
                name: "IX_Transactions_ActorUserId",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "ActorUserId",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "PlayersCanTakeFromStash",
                table: "Campaigns");

            migrationBuilder.DropColumn(
                name: "StashCopperPieces",
                table: "Campaigns");

            // Party stash records have no shop (or no character): they cannot survive the old schema.
            migrationBuilder.Sql("DELETE FROM \"Transactions\" WHERE \"ShopId\" IS NULL OR \"CharacterId\" IS NULL;");

            migrationBuilder.AlterColumn<Guid>(
                name: "ShopId",
                table: "Transactions",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "CharacterId",
                table: "Transactions",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
