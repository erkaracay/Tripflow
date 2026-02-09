using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tripflow.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class IncreaseLimitsAndAddProgramContent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Notes",
                table: "event_days",
                type: "character varying(15000)",
                maxLength: 15000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(2000)",
                oldMaxLength: 2000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Notes",
                table: "event_activities",
                type: "character varying(15000)",
                maxLength: 15000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(2000)",
                oldMaxLength: 2000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "MenuText",
                table: "event_activities",
                type: "character varying(15000)",
                maxLength: 15000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(2000)",
                oldMaxLength: 2000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Directions",
                table: "event_activities",
                type: "character varying(15000)",
                maxLength: 15000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(2000)",
                oldMaxLength: 2000,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProgramContent",
                table: "event_activities",
                type: "character varying(15000)",
                maxLength: 15000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProgramContent",
                table: "event_activities");

            migrationBuilder.AlterColumn<string>(
                name: "Notes",
                table: "event_days",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(15000)",
                oldMaxLength: 15000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Notes",
                table: "event_activities",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(15000)",
                oldMaxLength: 15000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "MenuText",
                table: "event_activities",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(15000)",
                oldMaxLength: 15000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Directions",
                table: "event_activities",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(15000)",
                oldMaxLength: 15000,
                oldNullable: true);
        }
    }
}
