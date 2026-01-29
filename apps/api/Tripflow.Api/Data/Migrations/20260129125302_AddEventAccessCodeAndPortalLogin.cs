using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tripflow.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEventAccessCodeAndPortalLogin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "EventId",
                table: "portal_sessions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastSeenAt",
                table: "portal_sessions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TokenHash",
                table: "portal_sessions",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EventAccessCode",
                table: "events",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE events
                SET "EventAccessCode" = substring(upper(replace(gen_random_uuid()::text, '-', '')) from 1 for 8)
                WHERE "EventAccessCode" IS NULL OR "EventAccessCode" = '';
                """);

            migrationBuilder.Sql("""
                UPDATE portal_sessions
                SET "EventId" = p."EventId",
                    "TokenHash" = substring(md5("portal_sessions"."Id"::text) from 1 for 64),
                    "LastSeenAt" = now()
                FROM participants p
                WHERE "portal_sessions"."ParticipantId" = p."Id";
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "EventId",
                table: "portal_sessions",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "LastSeenAt",
                table: "portal_sessions",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "TokenHash",
                table: "portal_sessions",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(128)",
                oldMaxLength: 128,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "EventAccessCode",
                table: "events",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(16)",
                oldMaxLength: 16,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_portal_sessions_EventId_ParticipantId",
                table: "portal_sessions",
                columns: new[] { "EventId", "ParticipantId" });

            migrationBuilder.CreateIndex(
                name: "IX_portal_sessions_ExpiresAt",
                table: "portal_sessions",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_portal_sessions_LastSeenAt",
                table: "portal_sessions",
                column: "LastSeenAt");

            migrationBuilder.CreateIndex(
                name: "IX_portal_sessions_TokenHash",
                table: "portal_sessions",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_events_OrganizationId_EventAccessCode",
                table: "events",
                columns: new[] { "OrganizationId", "EventAccessCode" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_portal_sessions_events_EventId",
                table: "portal_sessions",
                column: "EventId",
                principalTable: "events",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_portal_sessions_events_EventId",
                table: "portal_sessions");

            migrationBuilder.DropIndex(
                name: "IX_portal_sessions_EventId_ParticipantId",
                table: "portal_sessions");

            migrationBuilder.DropIndex(
                name: "IX_portal_sessions_ExpiresAt",
                table: "portal_sessions");

            migrationBuilder.DropIndex(
                name: "IX_portal_sessions_LastSeenAt",
                table: "portal_sessions");

            migrationBuilder.DropIndex(
                name: "IX_portal_sessions_TokenHash",
                table: "portal_sessions");

            migrationBuilder.DropIndex(
                name: "IX_events_OrganizationId_EventAccessCode",
                table: "events");

            migrationBuilder.DropColumn(
                name: "EventId",
                table: "portal_sessions");

            migrationBuilder.DropColumn(
                name: "LastSeenAt",
                table: "portal_sessions");

            migrationBuilder.DropColumn(
                name: "TokenHash",
                table: "portal_sessions");

            migrationBuilder.DropColumn(
                name: "EventAccessCode",
                table: "events");
        }
    }
}
