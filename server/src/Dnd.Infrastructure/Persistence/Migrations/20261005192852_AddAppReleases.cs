using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dnd.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAppReleases : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppReleases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    BuildNumber = table.Column<int>(type: "integer", nullable: false),
                    FileId = table.Column<Guid>(type: "uuid", nullable: false),
                    Notes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    IsMandatory = table.Column<bool>(type: "boolean", nullable: false),
                    PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppReleases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppReleases_StoredFiles_FileId",
                        column: x => x.FileId,
                        principalTable: "StoredFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppReleases_BuildNumber",
                table: "AppReleases",
                column: "BuildNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppReleases_FileId",
                table: "AppReleases",
                column: "FileId");

            migrationBuilder.CreateIndex(
                name: "IX_AppReleases_Version",
                table: "AppReleases",
                column: "Version",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppReleases");
        }
    }
}
