using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dnd.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MakeDmCharactersNpcs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // A DM or the owner of a campaign has no characters of their own: theirs become NPCs.
            migrationBuilder.Sql(
                """
                UPDATE "Characters" AS c
                SET "OwnerUserId" = NULL
                WHERE c."OwnerUserId" IS NOT NULL
                  AND EXISTS (
                      SELECT 1 FROM "CampaignMembers" AS m
                      WHERE m."CampaignId" = c."CampaignId"
                        AND m."UserId" = c."OwnerUserId"
                        AND m."Role" IN ('DM', 'Owner'));
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Data only: the previous owners are not recorded, so nothing to undo.
        }
    }
}
