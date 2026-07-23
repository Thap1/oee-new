using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OeeNew.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSyncTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SiteSyncStatus",
                columns: table => new
                {
                    SiteId = table.Column<Guid>(type: "uuid", nullable: false),
                    LastSyncedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SiteSyncStatus", x => x.SiteId);
                    table.ForeignKey(
                        name: "FK_SiteSyncStatus_Site_SiteId",
                        column: x => x.SiteId,
                        principalTable: "Site",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SyncCursor",
                columns: table => new
                {
                    Id = table.Column<short>(type: "smallint", nullable: false),
                    LastPushedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SyncCursor", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SiteSyncStatus");

            migrationBuilder.DropTable(
                name: "SyncCursor");
        }
    }
}
