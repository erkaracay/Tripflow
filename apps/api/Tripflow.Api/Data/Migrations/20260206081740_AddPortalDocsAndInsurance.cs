using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tripflow.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPortalDocsAndInsurance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BoardType",
                table: "participant_details",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InsuranceCompanyName",
                table: "participant_details",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "InsuranceEndDate",
                table: "participant_details",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InsurancePolicyNo",
                table: "participant_details",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "InsuranceStartDate",
                table: "participant_details",
                type: "date",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "event_doc_tabs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    ContentJson = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'{}'::jsonb"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_event_doc_tabs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_event_doc_tabs_events_EventId",
                        column: x => x.EventId,
                        principalTable: "events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_event_doc_tabs_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_event_doc_tabs_EventId",
                table: "event_doc_tabs",
                column: "EventId");

            migrationBuilder.CreateIndex(
                name: "IX_event_doc_tabs_OrganizationId_EventId_SortOrder",
                table: "event_doc_tabs",
                columns: new[] { "OrganizationId", "EventId", "SortOrder" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "event_doc_tabs");

            migrationBuilder.DropColumn(
                name: "BoardType",
                table: "participant_details");

            migrationBuilder.DropColumn(
                name: "InsuranceCompanyName",
                table: "participant_details");

            migrationBuilder.DropColumn(
                name: "InsuranceEndDate",
                table: "participant_details");

            migrationBuilder.DropColumn(
                name: "InsurancePolicyNo",
                table: "participant_details");

            migrationBuilder.DropColumn(
                name: "InsuranceStartDate",
                table: "participant_details");
        }
    }
}
