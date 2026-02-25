using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tripflow.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class SplitParticipantNameFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FirstName",
                table: "participants",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastName",
                table: "participants",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.Sql(
                """
                WITH parsed AS (
                    SELECT
                        "Id",
                        BTRIM(REGEXP_REPLACE(COALESCE("FullName", ''), '\s+', ' ', 'g')) AS normalized
                    FROM participants
                )
                UPDATE participants AS p
                SET
                    "LastName" = COALESCE(
                        NULLIF(
                            SPLIT_PART(
                                parsed.normalized,
                                ' ',
                                ARRAY_LENGTH(REGEXP_SPLIT_TO_ARRAY(parsed.normalized, ' '), 1)
                            ),
                            ''
                        ),
                        'Unknown'
                    ),
                    "FirstName" = COALESCE(
                        NULLIF(
                            BTRIM(REGEXP_REPLACE(parsed.normalized, '\s+\S+$', '')),
                            ''
                        ),
                        NULLIF(
                            SPLIT_PART(
                                parsed.normalized,
                                ' ',
                                ARRAY_LENGTH(REGEXP_SPLIT_TO_ARRAY(parsed.normalized, ' '), 1)
                            ),
                            ''
                        ),
                        'Unknown'
                    )
                FROM parsed
                WHERE p."Id" = parsed."Id";
                """);

            migrationBuilder.Sql(
                """
                UPDATE participants
                SET "FullName" = BTRIM(CONCAT("FirstName", ' ', "LastName"));
                """);

            migrationBuilder.AlterColumn<string>(
                name: "FirstName",
                table: "participants",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "LastName",
                table: "participants",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FirstName",
                table: "participants");

            migrationBuilder.DropColumn(
                name: "LastName",
                table: "participants");
        }
    }
}
