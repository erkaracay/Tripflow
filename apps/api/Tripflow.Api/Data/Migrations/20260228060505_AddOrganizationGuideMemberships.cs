using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tripflow.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOrganizationGuideMemberships : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "organization_guides",
                columns: table => new
                {
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    GuideUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_organization_guides", x => new { x.OrganizationId, x.GuideUserId });
                    table.ForeignKey(
                        name: "FK_organization_guides_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_organization_guides_users_GuideUserId",
                        column: x => x.GuideUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_organization_guides_GuideUserId",
                table: "organization_guides",
                column: "GuideUserId");

            migrationBuilder.Sql(
                """
                INSERT INTO organization_guides ("OrganizationId", "GuideUserId", "CreatedAt")
                SELECT "OrganizationId", "Id", now()
                FROM users
                WHERE "Role" = 'Guide' AND "OrganizationId" IS NOT NULL
                ON CONFLICT ("OrganizationId", "GuideUserId") DO NOTHING;
                """);

            migrationBuilder.Sql(
                """
                UPDATE users
                SET "OrganizationId" = NULL
                WHERE "Role" = 'Guide';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE users AS u
                SET "OrganizationId" = memberships."OrganizationId"
                FROM (
                    SELECT "GuideUserId", MIN("OrganizationId") AS "OrganizationId"
                    FROM organization_guides
                    GROUP BY "GuideUserId"
                ) AS memberships
                WHERE u."Id" = memberships."GuideUserId"
                  AND u."Role" = 'Guide'
                  AND u."OrganizationId" IS NULL;
                """);

            migrationBuilder.DropTable(
                name: "organization_guides");
        }
    }
}
