using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenTrpg.Core.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// The rest requests keep the game system's payload instead of the D&amp;D 5e hit dice: <c>HitDiceJson</c>
    /// (<c>{"fighter":2}</c>) becomes <c>PayloadJson</c> (<c>{"hitDice":{"fighter":2}}</c>).
    /// </summary>
    /// <remarks>
    /// The differ also proposes making the D&amp;D 5e columns of <c>Characters</c> NOT NULL: the snapshot cannot express
    /// that the 5e part is a required dependent of the shared table, but the columns already are NOT NULL in the
    /// database, so those operations are left out.
    /// </remarks>
    public partial class RenameRestRequestPayload : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "HitDiceJson",
                table: "RestRequests",
                newName: "PayloadJson");

            migrationBuilder.Sql("""UPDATE "RestRequests" SET "PayloadJson" = '{"hitDice":' || "PayloadJson" || '}';""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""UPDATE "RestRequests" SET "PayloadJson" = COALESCE(("PayloadJson"::jsonb -> 'hitDice')::text, '{}');""");

            migrationBuilder.RenameColumn(
                name: "PayloadJson",
                table: "RestRequests",
                newName: "HitDiceJson");
        }
    }
}
