using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tripflow.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitDb : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "checkins",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TourId = table.Column<Guid>(type: "uuid", nullable: false),
                    ParticipantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CheckedInAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Method = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_checkins", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "tours",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tours", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "participants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TourId = table.Column<Guid>(type: "uuid", nullable: false),
                    FullName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    CheckInCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_participants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_participants_tours_TourId",
                        column: x => x.TourId,
                        principalTable: "tours",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tour_portals",
                columns: table => new
                {
                    TourId = table.Column<Guid>(type: "uuid", nullable: false),
                    PortalJson = table.Column<string>(type: "jsonb", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tour_portals", x => x.TourId);
                    table.ForeignKey(
                        name: "FK_tour_portals_tours_TourId",
                        column: x => x.TourId,
                        principalTable: "tours",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_checkins_ParticipantId",
                table: "checkins",
                column: "ParticipantId");

            migrationBuilder.CreateIndex(
                name: "IX_checkins_TourId",
                table: "checkins",
                column: "TourId");

            migrationBuilder.CreateIndex(
                name: "IX_checkins_TourId_ParticipantId",
                table: "checkins",
                columns: new[] { "TourId", "ParticipantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_participants_CheckInCode",
                table: "participants",
                column: "CheckInCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_participants_TourId",
                table: "participants",
                column: "TourId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "checkins");

            migrationBuilder.DropTable(
                name: "participants");

            migrationBuilder.DropTable(
                name: "tour_portals");

            migrationBuilder.DropTable(
                name: "tours");
        }
    }
}
