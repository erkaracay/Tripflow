using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tripflow.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddParticipantFlightSegments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "participant_flight_segments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    ParticipantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Direction = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    SegmentIndex = table.Column<int>(type: "integer", nullable: false),
                    Airline = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    DepartureAirport = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    ArrivalAirport = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    FlightCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    DepartureDate = table.Column<DateOnly>(type: "date", nullable: true),
                    DepartureTime = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    ArrivalDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ArrivalTime = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    Pnr = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    TicketNo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    BaggagePieces = table.Column<int>(type: "integer", nullable: true),
                    BaggageTotalKg = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_participant_flight_segments", x => x.Id);
                    table.CheckConstraint("CK_participant_flight_segments_baggage_pieces", "\"BaggagePieces\" IS NULL OR \"BaggagePieces\" > 0");
                    table.CheckConstraint("CK_participant_flight_segments_baggage_total_kg", "\"BaggageTotalKg\" IS NULL OR \"BaggageTotalKg\" > 0");
                    table.CheckConstraint("CK_participant_flight_segments_segment_index", "\"SegmentIndex\" >= 1");
                    table.ForeignKey(
                        name: "FK_participant_flight_segments_events_EventId",
                        column: x => x.EventId,
                        principalTable: "events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_participant_flight_segments_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_participant_flight_segments_participants_ParticipantId",
                        column: x => x.ParticipantId,
                        principalTable: "participants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_participant_flight_segments_EventId",
                table: "participant_flight_segments",
                column: "EventId");

            migrationBuilder.CreateIndex(
                name: "IX_participant_flight_segments_OrganizationId_EventId_Particip~",
                table: "participant_flight_segments",
                columns: new[] { "OrganizationId", "EventId", "ParticipantId", "Direction", "SegmentIndex" });

            migrationBuilder.CreateIndex(
                name: "IX_participant_flight_segments_ParticipantId_Direction_Segment~",
                table: "participant_flight_segments",
                columns: new[] { "ParticipantId", "Direction", "SegmentIndex" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "participant_flight_segments");
        }
    }
}
