using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tripflow.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddParticipantCoreAndDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Phone",
                table: "participants",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "BirthDate",
                table: "participants",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1));

            migrationBuilder.AddColumn<string>(
                name: "Gender",
                table: "participants",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TcNo",
                table: "participants",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "participant_details",
                columns: table => new
                {
                    ParticipantId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoomNo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    RoomType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    PersonNo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    AgencyName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    City = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    FlightCity = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    HotelCheckInDate = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    HotelCheckOutDate = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    TicketNo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    AttendanceStatus = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ArrivalAirline = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ArrivalDepartureAirport = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ArrivalArrivalAirport = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ArrivalFlightCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ArrivalDepartureTime = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ArrivalArrivalTime = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ArrivalPnr = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ArrivalBaggageAllowance = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ReturnAirline = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ReturnDepartureAirport = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ReturnArrivalAirport = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ReturnFlightCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ReturnDepartureTime = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ReturnArrivalTime = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ReturnPnr = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ReturnBaggageAllowance = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_participant_details", x => x.ParticipantId);
                    table.ForeignKey(
                        name: "FK_participant_details_participants_ParticipantId",
                        column: x => x.ParticipantId,
                        principalTable: "participants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_participants_EventId_TcNo",
                table: "participants",
                columns: new[] { "EventId", "TcNo" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "participant_details");

            migrationBuilder.DropIndex(
                name: "IX_participants_EventId_TcNo",
                table: "participants");

            migrationBuilder.DropColumn(
                name: "BirthDate",
                table: "participants");

            migrationBuilder.DropColumn(
                name: "Gender",
                table: "participants");

            migrationBuilder.DropColumn(
                name: "TcNo",
                table: "participants");

            migrationBuilder.AlterColumn<string>(
                name: "Phone",
                table: "participants",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);
        }
    }
}
