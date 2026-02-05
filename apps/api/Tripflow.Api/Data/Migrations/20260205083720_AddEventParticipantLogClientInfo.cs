using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tripflow.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEventParticipantLogClientInfo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "IpAddress",
                table: "event_participant_logs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UserAgent",
                table: "event_participant_logs",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IpAddress",
                table: "event_participant_logs");

            migrationBuilder.DropColumn(
                name: "UserAgent",
                table: "event_participant_logs");
        }
    }
}
