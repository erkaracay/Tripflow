using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tripflow.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMealMenuChoices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "activity_meal_groups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActivityId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    AllowOther = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    AllowNote = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_activity_meal_groups", x => x.Id);
                    table.CheckConstraint("CK_activity_meal_groups_sort_order", "\"SortOrder\" >= 1");
                    table.ForeignKey(
                        name: "FK_activity_meal_groups_event_activities_ActivityId",
                        column: x => x.ActivityId,
                        principalTable: "event_activities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_activity_meal_groups_events_EventId",
                        column: x => x.EventId,
                        principalTable: "events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_activity_meal_groups_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "activity_meal_options",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    Label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_activity_meal_options", x => x.Id);
                    table.CheckConstraint("CK_activity_meal_options_sort_order", "\"SortOrder\" >= 1");
                    table.ForeignKey(
                        name: "FK_activity_meal_options_activity_meal_groups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "activity_meal_groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_activity_meal_options_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "participant_meal_selections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActivityId = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    ParticipantId = table.Column<Guid>(type: "uuid", nullable: false),
                    OptionId = table.Column<Guid>(type: "uuid", nullable: true),
                    OtherText = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Note = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_participant_meal_selections", x => x.Id);
                    table.CheckConstraint("CK_participant_meal_selections_option_or_other", "\"OptionId\" IS NOT NULL OR \"OtherText\" IS NOT NULL");
                    table.ForeignKey(
                        name: "FK_participant_meal_selections_activity_meal_groups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "activity_meal_groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_participant_meal_selections_activity_meal_options_OptionId",
                        column: x => x.OptionId,
                        principalTable: "activity_meal_options",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_participant_meal_selections_event_activities_ActivityId",
                        column: x => x.ActivityId,
                        principalTable: "event_activities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_participant_meal_selections_events_EventId",
                        column: x => x.EventId,
                        principalTable: "events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_participant_meal_selections_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_participant_meal_selections_participants_ParticipantId",
                        column: x => x.ParticipantId,
                        principalTable: "participants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_activity_meal_groups_ActivityId",
                table: "activity_meal_groups",
                column: "ActivityId");

            migrationBuilder.CreateIndex(
                name: "IX_activity_meal_groups_EventId",
                table: "activity_meal_groups",
                column: "EventId");

            migrationBuilder.CreateIndex(
                name: "IX_activity_meal_groups_OrganizationId_ActivityId_SortOrder",
                table: "activity_meal_groups",
                columns: new[] { "OrganizationId", "ActivityId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_activity_meal_groups_OrganizationId_EventId_ActivityId",
                table: "activity_meal_groups",
                columns: new[] { "OrganizationId", "EventId", "ActivityId" });

            migrationBuilder.CreateIndex(
                name: "IX_activity_meal_options_GroupId",
                table: "activity_meal_options",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_activity_meal_options_OrganizationId_GroupId_SortOrder",
                table: "activity_meal_options",
                columns: new[] { "OrganizationId", "GroupId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_participant_meal_selections_ActivityId",
                table: "participant_meal_selections",
                column: "ActivityId");

            migrationBuilder.CreateIndex(
                name: "IX_participant_meal_selections_EventId",
                table: "participant_meal_selections",
                column: "EventId");

            migrationBuilder.CreateIndex(
                name: "IX_participant_meal_selections_GroupId",
                table: "participant_meal_selections",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_participant_meal_selections_OptionId",
                table: "participant_meal_selections",
                column: "OptionId");

            migrationBuilder.CreateIndex(
                name: "IX_participant_meal_selections_OrganizationId_ActivityId_Grou~1",
                table: "participant_meal_selections",
                columns: new[] { "OrganizationId", "ActivityId", "GroupId", "ParticipantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_participant_meal_selections_OrganizationId_ActivityId_Group~",
                table: "participant_meal_selections",
                columns: new[] { "OrganizationId", "ActivityId", "GroupId" });

            migrationBuilder.CreateIndex(
                name: "IX_participant_meal_selections_OrganizationId_EventId_Activity~",
                table: "participant_meal_selections",
                columns: new[] { "OrganizationId", "EventId", "ActivityId" });

            migrationBuilder.CreateIndex(
                name: "IX_participant_meal_selections_OrganizationId_ParticipantId",
                table: "participant_meal_selections",
                columns: new[] { "OrganizationId", "ParticipantId" });

            migrationBuilder.CreateIndex(
                name: "IX_participant_meal_selections_ParticipantId",
                table: "participant_meal_selections",
                column: "ParticipantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "participant_meal_selections");

            migrationBuilder.DropTable(
                name: "activity_meal_options");

            migrationBuilder.DropTable(
                name: "activity_meal_groups");
        }
    }
}
