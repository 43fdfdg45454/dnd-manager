using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenTrpg.Core.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddItemsAndShops : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Effects",
                table: "ItemTemplates",
                type: "text",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "Characters",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "CharacterItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CharacterId = table.Column<Guid>(type: "uuid", nullable: false),
                    CampaignId = table.Column<Guid>(type: "uuid", nullable: false),
                    TemplateId = table.Column<Guid>(type: "uuid", nullable: true),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    Equipped = table.Column<bool>(type: "boolean", nullable: false),
                    Attuned = table.Column<bool>(type: "boolean", nullable: false),
                    Charges = table.Column<int>(type: "integer", nullable: true),
                    ChargesMax = table.Column<int>(type: "integer", nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
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
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CharacterItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CharacterItems_Campaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "Campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CharacterItems_Characters_CharacterId",
                        column: x => x.CharacterId,
                        principalTable: "Characters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CharacterItems_ItemTemplates_TemplateId",
                        column: x => x.TemplateId,
                        principalTable: "ItemTemplates",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Shops",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CampaignId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: true),
                    IsOpen = table.Column<bool>(type: "boolean", nullable: false),
                    BuybackPercent = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Shops", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Shops_Campaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "Campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ShopItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ShopId = table.Column<Guid>(type: "uuid", nullable: false),
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
                    PriceCp = table.Column<int>(type: "integer", nullable: false),
                    Stock = table.Column<int>(type: "integer", nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShopItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ShopItems_ItemTemplates_TemplateId",
                        column: x => x.TemplateId,
                        principalTable: "ItemTemplates",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ShopItems_Shops_ShopId",
                        column: x => x.ShopId,
                        principalTable: "Shops",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Transactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CampaignId = table.Column<Guid>(type: "uuid", nullable: false),
                    ShopId = table.Column<Guid>(type: "uuid", nullable: false),
                    CharacterId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ItemName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    TotalCp = table.Column<int>(type: "integer", nullable: false),
                    At = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Transactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Transactions_Campaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "Campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Transactions_Characters_CharacterId",
                        column: x => x.CharacterId,
                        principalTable: "Characters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Transactions_Shops_ShopId",
                        column: x => x.ShopId,
                        principalTable: "Shops",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CharacterItems_CampaignId",
                table: "CharacterItems",
                column: "CampaignId");

            migrationBuilder.CreateIndex(
                name: "IX_CharacterItems_CharacterId",
                table: "CharacterItems",
                column: "CharacterId");

            migrationBuilder.CreateIndex(
                name: "IX_CharacterItems_TemplateId",
                table: "CharacterItems",
                column: "TemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_ShopItems_ShopId",
                table: "ShopItems",
                column: "ShopId");

            migrationBuilder.CreateIndex(
                name: "IX_ShopItems_TemplateId",
                table: "ShopItems",
                column: "TemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_Shops_CampaignId_Name",
                table: "Shops",
                columns: new[] { "CampaignId", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_CampaignId",
                table: "Transactions",
                column: "CampaignId");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_CharacterId",
                table: "Transactions",
                column: "CharacterId");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_ShopId",
                table: "Transactions",
                column: "ShopId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CharacterItems");

            migrationBuilder.DropTable(
                name: "ShopItems");

            migrationBuilder.DropTable(
                name: "Transactions");

            migrationBuilder.DropTable(
                name: "Shops");

            migrationBuilder.DropColumn(
                name: "Effects",
                table: "ItemTemplates");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "Characters");
        }
    }
}
