using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tripflow.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCabinBaggageFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ArrivalCabinBaggage",
                table: "participant_details",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReturnCabinBaggage",
                table: "participant_details",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ArrivalCabinBaggage",
                table: "participant_details");

            migrationBuilder.DropColumn(
                name: "ReturnCabinBaggage",
                table: "participant_details");
        }
    }
}
