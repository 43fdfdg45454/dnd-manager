using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenTrpg.Core.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CatalogBackgrounds",
                columns: table => new
                {
                    Index = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    FeatureName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    FeatureDescription = table.Column<string>(type: "text", nullable: false),
                    SkillProficiencies = table.Column<string>(type: "text", nullable: false),
                    StartingEquipmentText = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CatalogBackgrounds", x => x.Index);
                });

            migrationBuilder.CreateTable(
                name: "CatalogClasses",
                columns: table => new
                {
                    Index = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    HitDie = table.Column<int>(type: "integer", nullable: false),
                    SavingThrows = table.Column<string>(type: "text", nullable: false),
                    ProficiencyNames = table.Column<string>(type: "text", nullable: false),
                    SpellcastingAbility = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    IsSpellcaster = table.Column<bool>(type: "boolean", nullable: false),
                    SpellcastingLevel = table.Column<int>(type: "integer", nullable: false),
                    IsPactCaster = table.Column<bool>(type: "boolean", nullable: false),
                    SubclassFlavor = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    StartingEquipmentText = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CatalogClasses", x => x.Index);
                });

            migrationBuilder.CreateTable(
                name: "CatalogConditions",
                columns: table => new
                {
                    Index = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CatalogConditions", x => x.Index);
                });

            migrationBuilder.CreateTable(
                name: "CatalogImports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Ruleset = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    DatasetVersion = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ImportedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CountsJson = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CatalogImports", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CatalogRaces",
                columns: table => new
                {
                    Index = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Speed = table.Column<int>(type: "integer", nullable: false),
                    Size = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    AbilityBonusesJson = table.Column<string>(type: "text", nullable: false),
                    TraitIndexes = table.Column<string>(type: "text", nullable: false),
                    Languages = table.Column<string>(type: "text", nullable: false),
                    Age = table.Column<string>(type: "text", nullable: false),
                    Alignment = table.Column<string>(type: "text", nullable: false),
                    SizeDescription = table.Column<string>(type: "text", nullable: false),
                    SubraceIndexes = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CatalogRaces", x => x.Index);
                });

            migrationBuilder.CreateTable(
                name: "CatalogSkills",
                columns: table => new
                {
                    Index = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    AbilityIndex = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CatalogSkills", x => x.Index);
                });

            migrationBuilder.CreateTable(
                name: "CatalogSpells",
                columns: table => new
                {
                    Index = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    School = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CastingTime = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Range = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Components = table.Column<string>(type: "text", nullable: false),
                    Material = table.Column<string>(type: "text", nullable: true),
                    Duration = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Concentration = table.Column<bool>(type: "boolean", nullable: false),
                    Ritual = table.Column<bool>(type: "boolean", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    HigherLevel = table.Column<string>(type: "text", nullable: false),
                    ClassIndexes = table.Column<string>(type: "text", nullable: false),
                    SubclassIndexes = table.Column<string>(type: "text", nullable: false),
                    AttackType = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    DamageJson = table.Column<string>(type: "text", nullable: true),
                    DcAbility = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CatalogSpells", x => x.Index);
                });

            migrationBuilder.CreateTable(
                name: "CatalogTraits",
                columns: table => new
                {
                    Index = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    RaceIndexes = table.Column<string>(type: "text", nullable: false),
                    SubraceIndexes = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CatalogTraits", x => x.Index);
                });

            migrationBuilder.CreateTable(
                name: "ItemTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CampaignId = table.Column<Guid>(type: "uuid", nullable: true),
                    Index = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Category = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Subcategory = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Rarity = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    RequiresAttunement = table.Column<bool>(type: "boolean", nullable: false),
                    CostCp = table.Column<int>(type: "integer", nullable: true),
                    WeightLb = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    DamageDice = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    DamageType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    VersatileDice = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    Properties = table.Column<string>(type: "text", nullable: false),
                    RangeNormal = table.Column<int>(type: "integer", nullable: true),
                    RangeLong = table.Column<int>(type: "integer", nullable: true),
                    ArmorClassBase = table.Column<int>(type: "integer", nullable: true),
                    AddDexModifier = table.Column<bool>(type: "boolean", nullable: true),
                    MaxDexBonus = table.Column<int>(type: "integer", nullable: true),
                    StrengthMinimum = table.Column<int>(type: "integer", nullable: true),
                    StealthDisadvantage = table.Column<bool>(type: "boolean", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItemTemplates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ItemTemplates_Campaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "Campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CatalogClassLevels",
                columns: table => new
                {
                    Index = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ClassIndex = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    ProfBonus = table.Column<int>(type: "integer", nullable: false),
                    AbilityScoreBonuses = table.Column<int>(type: "integer", nullable: false),
                    FeatureIndexes = table.Column<string>(type: "text", nullable: false),
                    ClassSpecificJson = table.Column<string>(type: "text", nullable: false),
                    CantripsKnown = table.Column<int>(type: "integer", nullable: true),
                    SpellsKnown = table.Column<int>(type: "integer", nullable: true),
                    SpellSlots = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CatalogClassLevels", x => x.Index);
                    table.ForeignKey(
                        name: "FK_CatalogClassLevels_CatalogClasses_ClassIndex",
                        column: x => x.ClassIndex,
                        principalTable: "CatalogClasses",
                        principalColumn: "Index",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CatalogSubclasses",
                columns: table => new
                {
                    Index = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ClassIndex = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Flavor = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CatalogSubclasses", x => x.Index);
                    table.ForeignKey(
                        name: "FK_CatalogSubclasses_CatalogClasses_ClassIndex",
                        column: x => x.ClassIndex,
                        principalTable: "CatalogClasses",
                        principalColumn: "Index",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CatalogSubraces",
                columns: table => new
                {
                    Index = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    RaceIndex = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    AbilityBonusesJson = table.Column<string>(type: "text", nullable: false),
                    TraitIndexes = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CatalogSubraces", x => x.Index);
                    table.ForeignKey(
                        name: "FK_CatalogSubraces_CatalogRaces_RaceIndex",
                        column: x => x.RaceIndex,
                        principalTable: "CatalogRaces",
                        principalColumn: "Index",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CatalogFeatures",
                columns: table => new
                {
                    Index = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ClassIndex = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SubclassIndex = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CatalogFeatures", x => x.Index);
                    table.ForeignKey(
                        name: "FK_CatalogFeatures_CatalogClasses_ClassIndex",
                        column: x => x.ClassIndex,
                        principalTable: "CatalogClasses",
                        principalColumn: "Index",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CatalogFeatures_CatalogSubclasses_SubclassIndex",
                        column: x => x.SubclassIndex,
                        principalTable: "CatalogSubclasses",
                        principalColumn: "Index",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CatalogSubclassLevels",
                columns: table => new
                {
                    Index = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SubclassIndex = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    FeatureIndexes = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CatalogSubclassLevels", x => x.Index);
                    table.ForeignKey(
                        name: "FK_CatalogSubclassLevels_CatalogSubclasses_SubclassIndex",
                        column: x => x.SubclassIndex,
                        principalTable: "CatalogSubclasses",
                        principalColumn: "Index",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CatalogBackgrounds_Name",
                table: "CatalogBackgrounds",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_CatalogClasses_Name",
                table: "CatalogClasses",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_CatalogClassLevels_ClassIndex_Level",
                table: "CatalogClassLevels",
                columns: new[] { "ClassIndex", "Level" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CatalogConditions_Name",
                table: "CatalogConditions",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_CatalogFeatures_ClassIndex_Level",
                table: "CatalogFeatures",
                columns: new[] { "ClassIndex", "Level" });

            migrationBuilder.CreateIndex(
                name: "IX_CatalogFeatures_Name",
                table: "CatalogFeatures",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_CatalogFeatures_SubclassIndex",
                table: "CatalogFeatures",
                column: "SubclassIndex");

            migrationBuilder.CreateIndex(
                name: "IX_CatalogImports_Ruleset_DatasetVersion",
                table: "CatalogImports",
                columns: new[] { "Ruleset", "DatasetVersion" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CatalogRaces_Name",
                table: "CatalogRaces",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_CatalogSkills_Name",
                table: "CatalogSkills",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_CatalogSpells_Level",
                table: "CatalogSpells",
                column: "Level");

            migrationBuilder.CreateIndex(
                name: "IX_CatalogSpells_Name",
                table: "CatalogSpells",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_CatalogSubclasses_ClassIndex",
                table: "CatalogSubclasses",
                column: "ClassIndex");

            migrationBuilder.CreateIndex(
                name: "IX_CatalogSubclasses_Name",
                table: "CatalogSubclasses",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_CatalogSubclassLevels_SubclassIndex_Level",
                table: "CatalogSubclassLevels",
                columns: new[] { "SubclassIndex", "Level" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CatalogSubraces_Name",
                table: "CatalogSubraces",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_CatalogSubraces_RaceIndex",
                table: "CatalogSubraces",
                column: "RaceIndex");

            migrationBuilder.CreateIndex(
                name: "IX_CatalogTraits_Name",
                table: "CatalogTraits",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_ItemTemplates_CampaignId_Name",
                table: "ItemTemplates",
                columns: new[] { "CampaignId", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_ItemTemplates_Category",
                table: "ItemTemplates",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_ItemTemplates_Index",
                table: "ItemTemplates",
                column: "Index",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ItemTemplates_Name",
                table: "ItemTemplates",
                column: "Name");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CatalogBackgrounds");

            migrationBuilder.DropTable(
                name: "CatalogClassLevels");

            migrationBuilder.DropTable(
                name: "CatalogConditions");

            migrationBuilder.DropTable(
                name: "CatalogFeatures");

            migrationBuilder.DropTable(
                name: "CatalogImports");

            migrationBuilder.DropTable(
                name: "CatalogSkills");

            migrationBuilder.DropTable(
                name: "CatalogSpells");

            migrationBuilder.DropTable(
                name: "CatalogSubclassLevels");

            migrationBuilder.DropTable(
                name: "CatalogSubraces");

            migrationBuilder.DropTable(
                name: "CatalogTraits");

            migrationBuilder.DropTable(
                name: "ItemTemplates");

            migrationBuilder.DropTable(
                name: "CatalogSubclasses");

            migrationBuilder.DropTable(
                name: "CatalogRaces");

            migrationBuilder.DropTable(
                name: "CatalogClasses");
        }
    }
}
