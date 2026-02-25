using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tripflow.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEventContacts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EmergencyPhone",
                table: "events",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GuideName",
                table: "events",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GuidePhone",
                table: "events",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LeaderName",
                table: "events",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LeaderPhone",
                table: "events",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WhatsappGroupUrl",
                table: "events",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EmergencyPhone",
                table: "events");

            migrationBuilder.DropColumn(
                name: "GuideName",
                table: "events");

            migrationBuilder.DropColumn(
                name: "GuidePhone",
                table: "events");

            migrationBuilder.DropColumn(
                name: "LeaderName",
                table: "events");

            migrationBuilder.DropColumn(
                name: "LeaderPhone",
                table: "events");

            migrationBuilder.DropColumn(
                name: "WhatsappGroupUrl",
                table: "events");
        }
    }
}
