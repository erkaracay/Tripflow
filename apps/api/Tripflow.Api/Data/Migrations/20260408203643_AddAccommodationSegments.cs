using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tripflow.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAccommodationSegments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "event_accommodation_segments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    DefaultAccommodationDocTabId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_event_accommodation_segments", x => x.Id);
                    table.CheckConstraint("CK_event_accommodation_segments_date_range", "\"EndDate\" >= \"StartDate\"");
                    table.ForeignKey(
                        name: "FK_event_accommodation_segments_event_doc_tabs_DefaultAccommod~",
                        column: x => x.DefaultAccommodationDocTabId,
                        principalTable: "event_doc_tabs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_event_accommodation_segments_events_EventId",
                        column: x => x.EventId,
                        principalTable: "events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "participant_accommodation_assignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    ParticipantId = table.Column<Guid>(type: "uuid", nullable: false),
                    SegmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    OverrideAccommodationDocTabId = table.Column<Guid>(type: "uuid", nullable: true),
                    RoomNo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    RoomType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    BoardType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    PersonNo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_participant_accommodation_assignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_participant_accommodation_assignments_event_accommodation_s~",
                        column: x => x.SegmentId,
                        principalTable: "event_accommodation_segments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_participant_accommodation_assignments_event_doc_tabs_Overri~",
                        column: x => x.OverrideAccommodationDocTabId,
                        principalTable: "event_doc_tabs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_participant_accommodation_assignments_participants_Particip~",
                        column: x => x.ParticipantId,
                        principalTable: "participants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_event_accommodation_segments_DefaultAccommodationDocTabId",
                table: "event_accommodation_segments",
                column: "DefaultAccommodationDocTabId");

            migrationBuilder.CreateIndex(
                name: "IX_event_accommodation_segments_EventId",
                table: "event_accommodation_segments",
                column: "EventId");

            migrationBuilder.CreateIndex(
                name: "IX_event_accommodation_segments_OrganizationId_EventId_Default~",
                table: "event_accommodation_segments",
                columns: new[] { "OrganizationId", "EventId", "DefaultAccommodationDocTabId" });

            migrationBuilder.CreateIndex(
                name: "IX_event_accommodation_segments_OrganizationId_EventId_StartDa~",
                table: "event_accommodation_segments",
                columns: new[] { "OrganizationId", "EventId", "StartDate", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_participant_accommodation_assignments_OrganizationId_EventI~",
                table: "participant_accommodation_assignments",
                columns: new[] { "OrganizationId", "EventId", "SegmentId" });

            migrationBuilder.CreateIndex(
                name: "IX_participant_accommodation_assignments_OverrideAccommodation~",
                table: "participant_accommodation_assignments",
                column: "OverrideAccommodationDocTabId");

            migrationBuilder.CreateIndex(
                name: "IX_participant_accommodation_assignments_ParticipantId_Segment~",
                table: "participant_accommodation_assignments",
                columns: new[] { "ParticipantId", "SegmentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_participant_accommodation_assignments_SegmentId",
                table: "participant_accommodation_assignments",
                column: "SegmentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "participant_accommodation_assignments");

            migrationBuilder.DropTable(
                name: "event_accommodation_segments");
        }
    }
}
