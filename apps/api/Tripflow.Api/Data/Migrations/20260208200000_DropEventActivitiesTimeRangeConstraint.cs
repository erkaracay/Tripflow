using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tripflow.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class DropEventActivitiesTimeRangeConstraint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_event_activities_time_range",
                table: "event_activities");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "CK_event_activities_time_range",
                table: "event_activities",
                sql: "\"EndTime\" IS NULL OR \"StartTime\" IS NULL OR \"EndTime\" >= \"StartTime\"");
        }
    }
}
