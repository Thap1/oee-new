using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OeeNew.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDowntimeEventOpenUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DowntimeEvent_MachineId",
                table: "DowntimeEvent");

            migrationBuilder.CreateIndex(
                name: "IX_DowntimeEvent_MachineId_OpenOnly",
                table: "DowntimeEvent",
                column: "MachineId",
                unique: true,
                filter: "\"EndedAt\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DowntimeEvent_MachineId_OpenOnly",
                table: "DowntimeEvent");

            migrationBuilder.CreateIndex(
                name: "IX_DowntimeEvent_MachineId",
                table: "DowntimeEvent",
                column: "MachineId");
        }
    }
}
