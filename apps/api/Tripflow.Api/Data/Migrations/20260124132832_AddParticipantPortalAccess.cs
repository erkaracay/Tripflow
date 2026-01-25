using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tripflow.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddParticipantPortalAccess : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PortalFailedAttempts",
                table: "participants",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "PortalLastFailedAt",
                table: "participants",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PortalLockedUntil",
                table: "participants",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "participant_access",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ParticipantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    Secret = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    SecretHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RevokedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_participant_access", x => x.Id);
                    table.ForeignKey(
                        name: "FK_participant_access_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_participant_access_participants_ParticipantId",
                        column: x => x.ParticipantId,
                        principalTable: "participants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "portal_sessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ParticipantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_portal_sessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_portal_sessions_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_portal_sessions_participants_ParticipantId",
                        column: x => x.ParticipantId,
                        principalTable: "participants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_participant_access_OrganizationId",
                table: "participant_access",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_participant_access_ParticipantId",
                table: "participant_access",
                column: "ParticipantId");

            migrationBuilder.CreateIndex(
                name: "IX_portal_sessions_OrganizationId",
                table: "portal_sessions",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_portal_sessions_ParticipantId",
                table: "portal_sessions",
                column: "ParticipantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "participant_access");

            migrationBuilder.DropTable(
                name: "portal_sessions");

            migrationBuilder.DropColumn(
                name: "PortalFailedAttempts",
                table: "participants");

            migrationBuilder.DropColumn(
                name: "PortalLastFailedAt",
                table: "participants");

            migrationBuilder.DropColumn(
                name: "PortalLockedUntil",
                table: "participants");
        }
    }
}
