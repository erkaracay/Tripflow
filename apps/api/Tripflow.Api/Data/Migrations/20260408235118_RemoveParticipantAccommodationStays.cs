using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tripflow.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveParticipantAccommodationStays : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "participant_accommodation_stays");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "participant_accommodation_stays",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventAccommodationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ParticipantId = table.Column<Guid>(type: "uuid", nullable: false),
                    BoardType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    CheckIn = table.Column<DateOnly>(type: "date", nullable: true),
                    CheckOut = table.Column<DateOnly>(type: "date", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    PersonNo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    RoomNo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    RoomType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_participant_accommodation_stays", x => x.Id);
                    table.ForeignKey(
                        name: "FK_participant_accommodation_stays_event_doc_tabs_EventAccommo~",
                        column: x => x.EventAccommodationId,
                        principalTable: "event_doc_tabs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_participant_accommodation_stays_participants_ParticipantId",
                        column: x => x.ParticipantId,
                        principalTable: "participants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_participant_accommodation_stays_EventAccommodationId",
                table: "participant_accommodation_stays",
                column: "EventAccommodationId");

            migrationBuilder.CreateIndex(
                name: "IX_participant_accommodation_stays_OrganizationId_EventId_Part~",
                table: "participant_accommodation_stays",
                columns: new[] { "OrganizationId", "EventId", "ParticipantId" });

            migrationBuilder.CreateIndex(
                name: "IX_participant_accommodation_stays_ParticipantId_EventAccommod~",
                table: "participant_accommodation_stays",
                columns: new[] { "ParticipantId", "EventAccommodationId" });
        }
    }
}
