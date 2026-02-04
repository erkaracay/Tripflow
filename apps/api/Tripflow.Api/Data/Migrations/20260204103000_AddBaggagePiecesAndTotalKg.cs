using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tripflow.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBaggagePiecesAndTotalKg : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ArrivalBaggagePieces",
                table: "participant_details",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ArrivalBaggageTotalKg",
                table: "participant_details",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReturnBaggagePieces",
                table: "participant_details",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReturnBaggageTotalKg",
                table: "participant_details",
                type: "integer",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_participant_details_arrival_baggage_pieces",
                table: "participant_details",
                sql: "\"ArrivalBaggagePieces\" IS NULL OR \"ArrivalBaggagePieces\" > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_participant_details_arrival_baggage_total_kg",
                table: "participant_details",
                sql: "\"ArrivalBaggageTotalKg\" IS NULL OR \"ArrivalBaggageTotalKg\" > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_participant_details_return_baggage_pieces",
                table: "participant_details",
                sql: "\"ReturnBaggagePieces\" IS NULL OR \"ReturnBaggagePieces\" > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_participant_details_return_baggage_total_kg",
                table: "participant_details",
                sql: "\"ReturnBaggageTotalKg\" IS NULL OR \"ReturnBaggageTotalKg\" > 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_participant_details_arrival_baggage_pieces",
                table: "participant_details");

            migrationBuilder.DropCheckConstraint(
                name: "CK_participant_details_arrival_baggage_total_kg",
                table: "participant_details");

            migrationBuilder.DropCheckConstraint(
                name: "CK_participant_details_return_baggage_pieces",
                table: "participant_details");

            migrationBuilder.DropCheckConstraint(
                name: "CK_participant_details_return_baggage_total_kg",
                table: "participant_details");

            migrationBuilder.DropColumn(
                name: "ArrivalBaggagePieces",
                table: "participant_details");

            migrationBuilder.DropColumn(
                name: "ArrivalBaggageTotalKg",
                table: "participant_details");

            migrationBuilder.DropColumn(
                name: "ReturnBaggagePieces",
                table: "participant_details");

            migrationBuilder.DropColumn(
                name: "ReturnBaggageTotalKg",
                table: "participant_details");
        }
    }
}
