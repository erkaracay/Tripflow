using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tripflow.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddParticipantAccommodationDocTabId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AccommodationDocTabId",
                table: "participant_details",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_participant_details_AccommodationDocTabId",
                table: "participant_details",
                column: "AccommodationDocTabId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_participant_details_AccommodationDocTabId",
                table: "participant_details");

            migrationBuilder.DropColumn(
                name: "AccommodationDocTabId",
                table: "participant_details");
        }
    }
}
