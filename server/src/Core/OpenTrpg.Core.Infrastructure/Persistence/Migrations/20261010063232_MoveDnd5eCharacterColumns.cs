using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenTrpg.Core.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Moves the D&amp;D 5e columns of <c>Characters</c> to their own table <c>Dnd5eCharacters</c> (key
    /// <c>CharacterId</c>, cascade from <c>Characters</c>), copying the existing rows, and points the 5e child tables
    /// of the sheet at it. Down copies them back and restores the NOT NULL columns.
    /// </summary>
    public partial class MoveDnd5eCharacterColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CharacterChoices_Characters_CharacterId",
                table: "CharacterChoices");

            migrationBuilder.DropForeignKey(
                name: "FK_CharacterClassLevels_Characters_CharacterId",
                table: "CharacterClassLevels");

            migrationBuilder.DropForeignKey(
                name: "FK_CharacterCompanions_Characters_CharacterId",
                table: "CharacterCompanions");

            migrationBuilder.DropForeignKey(
                name: "FK_CharacterOverrides_Characters_CharacterId",
                table: "CharacterOverrides");

            migrationBuilder.DropForeignKey(
                name: "FK_CharacterProficiencies_Characters_CharacterId",
                table: "CharacterProficiencies");

            migrationBuilder.DropForeignKey(
                name: "FK_CharacterResources_Characters_CharacterId",
                table: "CharacterResources");

            migrationBuilder.DropForeignKey(
                name: "FK_Characters_Users_LevelGrantedByUserId",
                table: "Characters");

            migrationBuilder.DropForeignKey(
                name: "FK_CharacterSpells_Characters_CharacterId",
                table: "CharacterSpells");

            migrationBuilder.DropForeignKey(
                name: "FK_CharacterSpellSlots_Characters_CharacterId",
                table: "CharacterSpellSlots");

            migrationBuilder.DropIndex(
                name: "IX_Characters_LevelGrantedByUserId",
                table: "Characters");

            migrationBuilder.CreateTable(
                name: "Dnd5eCharacters",
                columns: table => new
                {
                    CharacterId = table.Column<Guid>(type: "uuid", nullable: false),
                    RaceIndex = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    SubraceIndex = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    BackgroundIndex = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Alignment = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ApplyRacialBonuses = table.Column<bool>(type: "boolean", nullable: false),
                    HpMode = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    BaseStr = table.Column<int>(type: "integer", nullable: false),
                    BaseDex = table.Column<int>(type: "integer", nullable: false),
                    BaseCon = table.Column<int>(type: "integer", nullable: false),
                    BaseInt = table.Column<int>(type: "integer", nullable: false),
                    BaseWis = table.Column<int>(type: "integer", nullable: false),
                    BaseCha = table.Column<int>(type: "integer", nullable: false),
                    HitPointsCurrent = table.Column<int>(type: "integer", nullable: false),
                    TemporaryHitPoints = table.Column<int>(type: "integer", nullable: false),
                    DeathSaveSuccesses = table.Column<int>(type: "integer", nullable: false),
                    DeathSaveFailures = table.Column<int>(type: "integer", nullable: false),
                    ExhaustionLevel = table.Column<int>(type: "integer", nullable: false),
                    ConditionsJson = table.Column<string>(type: "text", nullable: false),
                    ConcentratingOnSpellIndex = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Inspiration = table.Column<bool>(type: "boolean", nullable: false),
                    HitDiceUsedJson = table.Column<string>(type: "text", nullable: false),
                    BackgroundDetail = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PendingLevelUpTo = table.Column<int>(type: "integer", nullable: true),
                    LevelGrantedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    LevelGrantedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    SpellPreparationPending = table.Column<bool>(type: "boolean", nullable: false),
                    SpellPreparationReason = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Dnd5eCharacters", x => x.CharacterId);
                    table.ForeignKey(
                        name: "FK_Dnd5eCharacters_Characters_CharacterId",
                        column: x => x.CharacterId,
                        principalTable: "Characters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Dnd5eCharacters_Users_LevelGrantedByUserId",
                        column: x => x.LevelGrantedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Dnd5eCharacters_LevelGrantedByUserId",
                table: "Dnd5eCharacters",
                column: "LevelGrantedByUserId");

            // Every character has its 5e part: copy the columns before dropping them from Characters.
            migrationBuilder.Sql(
                "INSERT INTO \"Dnd5eCharacters\" (\"CharacterId\", \"RaceIndex\", \"SubraceIndex\", \"BackgroundIndex\", \"Alignment\", \"ApplyRacialBonuses\", \"HpMode\", \"BaseStr\", \"BaseDex\", \"BaseCon\", \"BaseInt\", \"BaseWis\", \"BaseCha\", \"HitPointsCurrent\", \"TemporaryHitPoints\", \"DeathSaveSuccesses\", \"DeathSaveFailures\", \"ExhaustionLevel\", \"ConditionsJson\", \"ConcentratingOnSpellIndex\", \"Inspiration\", \"HitDiceUsedJson\", \"BackgroundDetail\", \"PendingLevelUpTo\", \"LevelGrantedByUserId\", \"LevelGrantedAt\", \"SpellPreparationPending\", \"SpellPreparationReason\") " +
                "SELECT \"Id\", \"RaceIndex\", \"SubraceIndex\", \"BackgroundIndex\", \"Alignment\", \"ApplyRacialBonuses\", \"HpMode\", \"BaseStr\", \"BaseDex\", \"BaseCon\", \"BaseInt\", \"BaseWis\", \"BaseCha\", \"HitPointsCurrent\", \"TemporaryHitPoints\", \"DeathSaveSuccesses\", \"DeathSaveFailures\", \"ExhaustionLevel\", \"ConditionsJson\", \"ConcentratingOnSpellIndex\", \"Inspiration\", \"HitDiceUsedJson\", \"BackgroundDetail\", \"PendingLevelUpTo\", \"LevelGrantedByUserId\", \"LevelGrantedAt\", \"SpellPreparationPending\", \"SpellPreparationReason\" FROM \"Characters\";");

            migrationBuilder.DropColumn(
                name: "Alignment",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "ApplyRacialBonuses",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "BackgroundDetail",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "BackgroundIndex",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "BaseCha",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "BaseCon",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "BaseDex",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "BaseInt",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "BaseStr",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "BaseWis",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "ConcentratingOnSpellIndex",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "ConditionsJson",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "DeathSaveFailures",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "DeathSaveSuccesses",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "ExhaustionLevel",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "HitDiceUsedJson",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "HitPointsCurrent",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "HpMode",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "Inspiration",
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

            migrationBuilder.DropColumn(
                name: "RaceIndex",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "SpellPreparationPending",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "SpellPreparationReason",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "SubraceIndex",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "TemporaryHitPoints",
                table: "Characters");

            migrationBuilder.AddForeignKey(
                name: "FK_CharacterChoices_Dnd5eCharacters_CharacterId",
                table: "CharacterChoices",
                column: "CharacterId",
                principalTable: "Dnd5eCharacters",
                principalColumn: "CharacterId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_CharacterClassLevels_Dnd5eCharacters_CharacterId",
                table: "CharacterClassLevels",
                column: "CharacterId",
                principalTable: "Dnd5eCharacters",
                principalColumn: "CharacterId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_CharacterCompanions_Dnd5eCharacters_CharacterId",
                table: "CharacterCompanions",
                column: "CharacterId",
                principalTable: "Dnd5eCharacters",
                principalColumn: "CharacterId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_CharacterOverrides_Dnd5eCharacters_CharacterId",
                table: "CharacterOverrides",
                column: "CharacterId",
                principalTable: "Dnd5eCharacters",
                principalColumn: "CharacterId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_CharacterProficiencies_Dnd5eCharacters_CharacterId",
                table: "CharacterProficiencies",
                column: "CharacterId",
                principalTable: "Dnd5eCharacters",
                principalColumn: "CharacterId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_CharacterResources_Dnd5eCharacters_CharacterId",
                table: "CharacterResources",
                column: "CharacterId",
                principalTable: "Dnd5eCharacters",
                principalColumn: "CharacterId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_CharacterSpells_Dnd5eCharacters_CharacterId",
                table: "CharacterSpells",
                column: "CharacterId",
                principalTable: "Dnd5eCharacters",
                principalColumn: "CharacterId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_CharacterSpellSlots_Dnd5eCharacters_CharacterId",
                table: "CharacterSpellSlots",
                column: "CharacterId",
                principalTable: "Dnd5eCharacters",
                principalColumn: "CharacterId",
                onDelete: ReferentialAction.Cascade);
        
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Alignment",
                table: "Characters",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ApplyRacialBonuses",
                table: "Characters",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BackgroundDetail",
                table: "Characters",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BackgroundIndex",
                table: "Characters",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BaseCha",
                table: "Characters",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BaseCon",
                table: "Characters",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BaseDex",
                table: "Characters",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BaseInt",
                table: "Characters",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BaseStr",
                table: "Characters",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BaseWis",
                table: "Characters",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ConcentratingOnSpellIndex",
                table: "Characters",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ConditionsJson",
                table: "Characters",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DeathSaveFailures",
                table: "Characters",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DeathSaveSuccesses",
                table: "Characters",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ExhaustionLevel",
                table: "Characters",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HitDiceUsedJson",
                table: "Characters",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "HitPointsCurrent",
                table: "Characters",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HpMode",
                table: "Characters",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Inspiration",
                table: "Characters",
                type: "boolean",
                nullable: true);

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

            migrationBuilder.AddColumn<string>(
                name: "RaceIndex",
                table: "Characters",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "SpellPreparationPending",
                table: "Characters",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SpellPreparationReason",
                table: "Characters",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SubraceIndex",
                table: "Characters",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TemporaryHitPoints",
                table: "Characters",
                type: "integer",
                nullable: true);

            // Copy the 5e part back before dropping its table.
            migrationBuilder.Sql(
                "UPDATE \"Characters\" AS c SET \"RaceIndex\" = d.\"RaceIndex\", \"SubraceIndex\" = d.\"SubraceIndex\", \"BackgroundIndex\" = d.\"BackgroundIndex\", \"Alignment\" = d.\"Alignment\", \"ApplyRacialBonuses\" = d.\"ApplyRacialBonuses\", \"HpMode\" = d.\"HpMode\", \"BaseStr\" = d.\"BaseStr\", \"BaseDex\" = d.\"BaseDex\", \"BaseCon\" = d.\"BaseCon\", \"BaseInt\" = d.\"BaseInt\", \"BaseWis\" = d.\"BaseWis\", \"BaseCha\" = d.\"BaseCha\", \"HitPointsCurrent\" = d.\"HitPointsCurrent\", \"TemporaryHitPoints\" = d.\"TemporaryHitPoints\", \"DeathSaveSuccesses\" = d.\"DeathSaveSuccesses\", \"DeathSaveFailures\" = d.\"DeathSaveFailures\", \"ExhaustionLevel\" = d.\"ExhaustionLevel\", \"ConditionsJson\" = d.\"ConditionsJson\", \"ConcentratingOnSpellIndex\" = d.\"ConcentratingOnSpellIndex\", \"Inspiration\" = d.\"Inspiration\", \"HitDiceUsedJson\" = d.\"HitDiceUsedJson\", \"BackgroundDetail\" = d.\"BackgroundDetail\", \"PendingLevelUpTo\" = d.\"PendingLevelUpTo\", \"LevelGrantedByUserId\" = d.\"LevelGrantedByUserId\", \"LevelGrantedAt\" = d.\"LevelGrantedAt\", \"SpellPreparationPending\" = d.\"SpellPreparationPending\", \"SpellPreparationReason\" = d.\"SpellPreparationReason\" " +
                "FROM \"Dnd5eCharacters\" AS d WHERE d.\"CharacterId\" = c.\"Id\";");

            migrationBuilder.AlterColumn<bool>(
                name: "ApplyRacialBonuses",
                table: "Characters",
                type: "boolean",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "BackgroundDetail",
                table: "Characters",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "BaseCha",
                table: "Characters",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "BaseCon",
                table: "Characters",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "BaseDex",
                table: "Characters",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "BaseInt",
                table: "Characters",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "BaseStr",
                table: "Characters",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "BaseWis",
                table: "Characters",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ConditionsJson",
                table: "Characters",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "DeathSaveFailures",
                table: "Characters",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "DeathSaveSuccesses",
                table: "Characters",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "ExhaustionLevel",
                table: "Characters",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "HitDiceUsedJson",
                table: "Characters",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "HitPointsCurrent",
                table: "Characters",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "HpMode",
                table: "Characters",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(16)",
                oldMaxLength: 16,
                oldNullable: true);

            migrationBuilder.AlterColumn<bool>(
                name: "Inspiration",
                table: "Characters",
                type: "boolean",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldNullable: true);

            migrationBuilder.AlterColumn<bool>(
                name: "SpellPreparationPending",
                table: "Characters",
                type: "boolean",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "TemporaryHitPoints",
                table: "Characters",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.DropForeignKey(
                name: "FK_CharacterChoices_Dnd5eCharacters_CharacterId",
                table: "CharacterChoices");

            migrationBuilder.DropForeignKey(
                name: "FK_CharacterClassLevels_Dnd5eCharacters_CharacterId",
                table: "CharacterClassLevels");

            migrationBuilder.DropForeignKey(
                name: "FK_CharacterCompanions_Dnd5eCharacters_CharacterId",
                table: "CharacterCompanions");

            migrationBuilder.DropForeignKey(
                name: "FK_CharacterOverrides_Dnd5eCharacters_CharacterId",
                table: "CharacterOverrides");

            migrationBuilder.DropForeignKey(
                name: "FK_CharacterProficiencies_Dnd5eCharacters_CharacterId",
                table: "CharacterProficiencies");

            migrationBuilder.DropForeignKey(
                name: "FK_CharacterResources_Dnd5eCharacters_CharacterId",
                table: "CharacterResources");

            migrationBuilder.DropForeignKey(
                name: "FK_CharacterSpells_Dnd5eCharacters_CharacterId",
                table: "CharacterSpells");

            migrationBuilder.DropForeignKey(
                name: "FK_CharacterSpellSlots_Dnd5eCharacters_CharacterId",
                table: "CharacterSpellSlots");

            migrationBuilder.DropTable(
                name: "Dnd5eCharacters");

            migrationBuilder.CreateIndex(
                name: "IX_Characters_LevelGrantedByUserId",
                table: "Characters",
                column: "LevelGrantedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_CharacterChoices_Characters_CharacterId",
                table: "CharacterChoices",
                column: "CharacterId",
                principalTable: "Characters",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_CharacterClassLevels_Characters_CharacterId",
                table: "CharacterClassLevels",
                column: "CharacterId",
                principalTable: "Characters",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_CharacterCompanions_Characters_CharacterId",
                table: "CharacterCompanions",
                column: "CharacterId",
                principalTable: "Characters",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_CharacterOverrides_Characters_CharacterId",
                table: "CharacterOverrides",
                column: "CharacterId",
                principalTable: "Characters",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_CharacterProficiencies_Characters_CharacterId",
                table: "CharacterProficiencies",
                column: "CharacterId",
                principalTable: "Characters",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_CharacterResources_Characters_CharacterId",
                table: "CharacterResources",
                column: "CharacterId",
                principalTable: "Characters",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Characters_Users_LevelGrantedByUserId",
                table: "Characters",
                column: "LevelGrantedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CharacterSpells_Characters_CharacterId",
                table: "CharacterSpells",
                column: "CharacterId",
                principalTable: "Characters",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_CharacterSpellSlots_Characters_CharacterId",
                table: "CharacterSpellSlots",
                column: "CharacterId",
                principalTable: "Characters",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        
        }
    }
}
