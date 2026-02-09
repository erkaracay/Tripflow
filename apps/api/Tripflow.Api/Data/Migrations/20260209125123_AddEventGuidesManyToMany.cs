using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tripflow.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEventGuidesManyToMany : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Create event_guides junction table first
            migrationBuilder.CreateTable(
                name: "event_guides",
                columns: table => new
                {
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    GuideUserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_event_guides", x => new { x.EventId, x.GuideUserId });
                    table.ForeignKey(
                        name: "FK_event_guides_events_EventId",
                        column: x => x.EventId,
                        principalTable: "events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_event_guides_users_GuideUserId",
                        column: x => x.GuideUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_event_guides_EventId",
                table: "event_guides",
                column: "EventId");

            migrationBuilder.CreateIndex(
                name: "IX_event_guides_GuideUserId",
                table: "event_guides",
                column: "GuideUserId");

            // Migrate existing GuideUserId values to event_guides table
            migrationBuilder.Sql(@"
                INSERT INTO event_guides (""EventId"", ""GuideUserId"")
                SELECT ""Id"", ""GuideUserId""
                FROM events
                WHERE ""GuideUserId"" IS NOT NULL;
            ");

            // Drop foreign key and column
            migrationBuilder.DropForeignKey(
                name: "FK_events_users_GuideUserId",
                table: "events");

            migrationBuilder.DropIndex(
                name: "IX_events_GuideUserId",
                table: "events");

            migrationBuilder.DropColumn(
                name: "GuideUserId",
                table: "events");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Add GuideUserId column back
            migrationBuilder.AddColumn<Guid>(
                name: "GuideUserId",
                table: "events",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_events_GuideUserId",
                table: "events",
                column: "GuideUserId");

            // Migrate data back (take first guide if multiple exist)
            migrationBuilder.Sql(@"
                UPDATE events e
                SET ""GuideUserId"" = (
                    SELECT ""GuideUserId""
                    FROM event_guides eg
                    WHERE eg.""EventId"" = e.""Id""
                    LIMIT 1
                );
            ");

            migrationBuilder.AddForeignKey(
                name: "FK_events_users_GuideUserId",
                table: "events",
                column: "GuideUserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.DropTable(
                name: "event_guides");
        }
    }
}
