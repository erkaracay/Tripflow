using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tripflow.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOrgPortalPolicy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "RequireLast4ForPortal",
                table: "organizations",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "RequireLast4ForQr",
                table: "organizations",
                type: "boolean",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RequireLast4ForPortal",
                table: "organizations");

            migrationBuilder.DropColumn(
                name: "RequireLast4ForQr",
                table: "organizations");
        }
    }
}
