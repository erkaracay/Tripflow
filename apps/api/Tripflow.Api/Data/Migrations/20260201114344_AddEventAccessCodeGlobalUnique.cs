using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tripflow.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEventAccessCodeGlobalUnique : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_events_OrganizationId_EventAccessCode",
                table: "events");

            migrationBuilder.CreateIndex(
                name: "IX_events_EventAccessCode",
                table: "events",
                column: "EventAccessCode",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_events_EventAccessCode",
                table: "events");

            migrationBuilder.CreateIndex(
                name: "IX_events_OrganizationId_EventAccessCode",
                table: "events",
                columns: new[] { "OrganizationId", "EventAccessCode" },
                unique: true);
        }
    }
}
