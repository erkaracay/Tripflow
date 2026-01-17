using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tripflow.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOrganizationsAndOrgScope : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            var defaultOrgId = new Guid("11111111-1111-1111-1111-111111111111");

            migrationBuilder.CreateTable(
                name: "organizations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Slug = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_organizations", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_organizations_Slug",
                table: "organizations",
                column: "Slug",
                unique: true);

            migrationBuilder.InsertData(
                table: "organizations",
                columns: new[] { "Id", "Name", "Slug", "CreatedAt" },
                values: new object[] { defaultOrgId, "Default Org", "default", new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) });

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "users",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "tours",
                type: "uuid",
                nullable: false,
                defaultValue: defaultOrgId);

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "tour_portals",
                type: "uuid",
                nullable: false,
                defaultValue: defaultOrgId);

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "participants",
                type: "uuid",
                nullable: false,
                defaultValue: defaultOrgId);

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "checkins",
                type: "uuid",
                nullable: false,
                defaultValue: defaultOrgId);

            migrationBuilder.CreateIndex(
                name: "IX_users_OrganizationId",
                table: "users",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_tours_OrganizationId_StartDate",
                table: "tours",
                columns: new[] { "OrganizationId", "StartDate" });

            migrationBuilder.CreateIndex(
                name: "IX_tour_portals_OrganizationId",
                table: "tour_portals",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_participants_OrganizationId",
                table: "participants",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_checkins_OrganizationId",
                table: "checkins",
                column: "OrganizationId");

            migrationBuilder.Sql("UPDATE users SET \"OrganizationId\" = '11111111-1111-1111-1111-111111111111' WHERE \"OrganizationId\" IS NULL;");
            migrationBuilder.Sql("UPDATE users SET \"Role\" = 'AgencyAdmin' WHERE \"Role\" = 'Admin';");

            migrationBuilder.AddForeignKey(
                name: "FK_checkins_organizations_OrganizationId",
                table: "checkins",
                column: "OrganizationId",
                principalTable: "organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_participants_organizations_OrganizationId",
                table: "participants",
                column: "OrganizationId",
                principalTable: "organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_tour_portals_organizations_OrganizationId",
                table: "tour_portals",
                column: "OrganizationId",
                principalTable: "organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_tours_organizations_OrganizationId",
                table: "tours",
                column: "OrganizationId",
                principalTable: "organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_users_organizations_OrganizationId",
                table: "users",
                column: "OrganizationId",
                principalTable: "organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_checkins_organizations_OrganizationId",
                table: "checkins");

            migrationBuilder.DropForeignKey(
                name: "FK_participants_organizations_OrganizationId",
                table: "participants");

            migrationBuilder.DropForeignKey(
                name: "FK_tour_portals_organizations_OrganizationId",
                table: "tour_portals");

            migrationBuilder.DropForeignKey(
                name: "FK_tours_organizations_OrganizationId",
                table: "tours");

            migrationBuilder.DropForeignKey(
                name: "FK_users_organizations_OrganizationId",
                table: "users");

            migrationBuilder.DropTable(
                name: "organizations");

            migrationBuilder.DropIndex(
                name: "IX_users_OrganizationId",
                table: "users");

            migrationBuilder.DropIndex(
                name: "IX_tours_OrganizationId_StartDate",
                table: "tours");

            migrationBuilder.DropIndex(
                name: "IX_tour_portals_OrganizationId",
                table: "tour_portals");

            migrationBuilder.DropIndex(
                name: "IX_participants_OrganizationId",
                table: "participants");

            migrationBuilder.DropIndex(
                name: "IX_checkins_OrganizationId",
                table: "checkins");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "users");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "tours");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "tour_portals");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "participants");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "checkins");
        }
    }
}
