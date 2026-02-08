using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tripflow.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddArrivalReturnTicketNo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ArrivalTicketNo",
                table: "participant_details",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReturnTicketNo",
                table: "participant_details",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE participant_details
                SET "ArrivalTicketNo" = "TicketNo"
                WHERE "ArrivalTicketNo" IS NULL AND "TicketNo" IS NOT NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ArrivalTicketNo",
                table: "participant_details");

            migrationBuilder.DropColumn(
                name: "ReturnTicketNo",
                table: "participant_details");
        }
    }
}
