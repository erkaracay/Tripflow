using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tripflow.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEventSchedule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "event_days",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_event_days", x => x.Id);
                    table.ForeignKey(
                        name: "FK_event_days_events_EventId",
                        column: x => x.EventId,
                        principalTable: "events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_event_days_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "event_activities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventDayId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    EndTime = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    LocationName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Directions = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CheckInEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    CheckInMode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false, defaultValue: "EntryOnly"),
                    MenuText = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    SurveyUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_event_activities", x => x.Id);
                    table.CheckConstraint("CK_event_activities_time_range", "\"EndTime\" IS NULL OR \"StartTime\" IS NULL OR \"EndTime\" >= \"StartTime\"");
                    table.ForeignKey(
                        name: "FK_event_activities_event_days_EventDayId",
                        column: x => x.EventDayId,
                        principalTable: "event_days",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_event_activities_events_EventId",
                        column: x => x.EventId,
                        principalTable: "events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_event_activities_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_event_activities_EventDayId",
                table: "event_activities",
                column: "EventDayId");

            migrationBuilder.CreateIndex(
                name: "IX_event_activities_EventId",
                table: "event_activities",
                column: "EventId");

            migrationBuilder.CreateIndex(
                name: "IX_event_activities_OrganizationId_EventDayId_StartTime",
                table: "event_activities",
                columns: new[] { "OrganizationId", "EventDayId", "StartTime" });

            migrationBuilder.CreateIndex(
                name: "IX_event_activities_OrganizationId_EventId",
                table: "event_activities",
                columns: new[] { "OrganizationId", "EventId" });

            migrationBuilder.CreateIndex(
                name: "IX_event_days_EventId",
                table: "event_days",
                column: "EventId");

            migrationBuilder.CreateIndex(
                name: "IX_event_days_OrganizationId_EventId_Date",
                table: "event_days",
                columns: new[] { "OrganizationId", "EventId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_event_days_OrganizationId_EventId_SortOrder",
                table: "event_days",
                columns: new[] { "OrganizationId", "EventId", "SortOrder" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "event_activities");

            migrationBuilder.DropTable(
                name: "event_days");
        }
    }
}
