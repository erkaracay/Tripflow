using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tripflow.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class FixParticipantDetailsTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "ALTER TABLE participant_details ALTER COLUMN \"ReturnDepartureTime\" TYPE time without time zone USING NULLIF(\"ReturnDepartureTime\", '')::time without time zone;");
            migrationBuilder.Sql(
                "ALTER TABLE participant_details ALTER COLUMN \"ReturnArrivalTime\" TYPE time without time zone USING NULLIF(\"ReturnArrivalTime\", '')::time without time zone;");
            migrationBuilder.Sql(
                "ALTER TABLE participant_details ALTER COLUMN \"ArrivalDepartureTime\" TYPE time without time zone USING NULLIF(\"ArrivalDepartureTime\", '')::time without time zone;");
            migrationBuilder.Sql(
                "ALTER TABLE participant_details ALTER COLUMN \"ArrivalArrivalTime\" TYPE time without time zone USING NULLIF(\"ArrivalArrivalTime\", '')::time without time zone;");
            migrationBuilder.Sql(
                "ALTER TABLE participant_details ALTER COLUMN \"HotelCheckInDate\" TYPE date USING NULLIF(\"HotelCheckInDate\", '')::date;");
            migrationBuilder.Sql(
                "ALTER TABLE participant_details ALTER COLUMN \"HotelCheckOutDate\" TYPE date USING NULLIF(\"HotelCheckOutDate\", '')::date;");

            migrationBuilder.AddCheckConstraint(
                name: "CK_participant_details_hotel_dates",
                table: "participant_details",
                sql: "\"HotelCheckOutDate\" IS NULL OR \"HotelCheckInDate\" IS NULL OR \"HotelCheckOutDate\" >= \"HotelCheckInDate\"");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_participant_details_hotel_dates",
                table: "participant_details");
            migrationBuilder.Sql(
                "ALTER TABLE participant_details ALTER COLUMN \"ReturnDepartureTime\" TYPE character varying(50) USING \"ReturnDepartureTime\"::text;");
            migrationBuilder.Sql(
                "ALTER TABLE participant_details ALTER COLUMN \"ReturnArrivalTime\" TYPE character varying(50) USING \"ReturnArrivalTime\"::text;");
            migrationBuilder.Sql(
                "ALTER TABLE participant_details ALTER COLUMN \"ArrivalDepartureTime\" TYPE character varying(50) USING \"ArrivalDepartureTime\"::text;");
            migrationBuilder.Sql(
                "ALTER TABLE participant_details ALTER COLUMN \"ArrivalArrivalTime\" TYPE character varying(50) USING \"ArrivalArrivalTime\"::text;");
            migrationBuilder.Sql(
                "ALTER TABLE participant_details ALTER COLUMN \"HotelCheckInDate\" TYPE character varying(50) USING \"HotelCheckInDate\"::text;");
            migrationBuilder.Sql(
                "ALTER TABLE participant_details ALTER COLUMN \"HotelCheckOutDate\" TYPE character varying(50) USING \"HotelCheckOutDate\"::text;");
        }
    }
}
