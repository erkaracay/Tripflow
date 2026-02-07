using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tripflow.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddParticipantTransferFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ArrivalTransferDriverInfo",
                table: "participant_details",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ArrivalTransferDropoffPlace",
                table: "participant_details",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ArrivalTransferNote",
                table: "participant_details",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ArrivalTransferPickupPlace",
                table: "participant_details",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "ArrivalTransferPickupTime",
                table: "participant_details",
                type: "time without time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ArrivalTransferPlate",
                table: "participant_details",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ArrivalTransferVehicle",
                table: "participant_details",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReturnTransferDriverInfo",
                table: "participant_details",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReturnTransferDropoffPlace",
                table: "participant_details",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReturnTransferNote",
                table: "participant_details",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReturnTransferPickupPlace",
                table: "participant_details",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "ReturnTransferPickupTime",
                table: "participant_details",
                type: "time without time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReturnTransferPlate",
                table: "participant_details",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReturnTransferVehicle",
                table: "participant_details",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ArrivalTransferDriverInfo",
                table: "participant_details");

            migrationBuilder.DropColumn(
                name: "ArrivalTransferDropoffPlace",
                table: "participant_details");

            migrationBuilder.DropColumn(
                name: "ArrivalTransferNote",
                table: "participant_details");

            migrationBuilder.DropColumn(
                name: "ArrivalTransferPickupPlace",
                table: "participant_details");

            migrationBuilder.DropColumn(
                name: "ArrivalTransferPickupTime",
                table: "participant_details");

            migrationBuilder.DropColumn(
                name: "ArrivalTransferPlate",
                table: "participant_details");

            migrationBuilder.DropColumn(
                name: "ArrivalTransferVehicle",
                table: "participant_details");

            migrationBuilder.DropColumn(
                name: "ReturnTransferDriverInfo",
                table: "participant_details");

            migrationBuilder.DropColumn(
                name: "ReturnTransferDropoffPlace",
                table: "participant_details");

            migrationBuilder.DropColumn(
                name: "ReturnTransferNote",
                table: "participant_details");

            migrationBuilder.DropColumn(
                name: "ReturnTransferPickupPlace",
                table: "participant_details");

            migrationBuilder.DropColumn(
                name: "ReturnTransferPickupTime",
                table: "participant_details");

            migrationBuilder.DropColumn(
                name: "ReturnTransferPlate",
                table: "participant_details");

            migrationBuilder.DropColumn(
                name: "ReturnTransferVehicle",
                table: "participant_details");
        }
    }
}
