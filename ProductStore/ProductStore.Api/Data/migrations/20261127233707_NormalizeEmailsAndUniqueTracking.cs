using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace productStore.Api.Data.migrations
{
    /// <inheritdoc />
    public partial class NormalizeEmailsAndUniqueTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE "Customers"
                SET "Email" = lower(btrim("Email"))
                WHERE "Email" <> lower(btrim("Email"));
                """);

            migrationBuilder.Sql("""
                UPDATE "Commands"
                SET "TrackingNumber" = upper(replace(gen_random_uuid()::text, '-', ''))
                WHERE "TrackingNumber" IS NULL
                   OR btrim("TrackingNumber") = ''
                   OR "TrackingNumber" !~ '^[0-9A-Fa-f]{32}$';
                """);

            migrationBuilder.Sql("""
                WITH duplicates AS (
                    SELECT "Id",
                           row_number() OVER (
                               PARTITION BY "TrackingNumber"
                               ORDER BY "CreatedAt", "Id"
                           ) AS position
                    FROM "Commands"
                    WHERE "TrackingNumber" IS NOT NULL AND "TrackingNumber" <> ''
                )
                UPDATE "Commands" AS command
                SET "TrackingNumber" = upper(replace(gen_random_uuid()::text, '-', ''))
                FROM duplicates
                WHERE command."Id" = duplicates."Id" AND duplicates.position > 1;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Commands_TrackingNumber",
                table: "Commands",
                column: "TrackingNumber",
                unique: true,
                filter: "\"TrackingNumber\" IS NOT NULL AND \"TrackingNumber\" <> ''");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Commands_TrackingNumber",
                table: "Commands");
        }
    }
}
