using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tripflow.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTransferSeatCompartment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ArrivalTransferCompartmentNo",
                table: "participant_details",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ArrivalTransferSeatNo",
                table: "participant_details",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReturnTransferCompartmentNo",
                table: "participant_details",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReturnTransferSeatNo",
                table: "participant_details",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ArrivalTransferCompartmentNo",
                table: "participant_details");

            migrationBuilder.DropColumn(
                name: "ArrivalTransferSeatNo",
                table: "participant_details");

            migrationBuilder.DropColumn(
                name: "ReturnTransferCompartmentNo",
                table: "participant_details");

            migrationBuilder.DropColumn(
                name: "ReturnTransferSeatNo",
                table: "participant_details");
        }
    }
}
