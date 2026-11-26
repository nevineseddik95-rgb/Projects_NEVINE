using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace productStore.Api.Data.migrations
{
    /// <inheritdoc />
    public partial class OrderWorkflowAndPersistentImages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CancelledAt",
                table: "Commands",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "Commands",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.AddColumn<DateTime>(
                name: "DeliveredAt",
                table: "Commands",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StatusNote",
                table: "Commands",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "StatusUpdatedAt",
                table: "Commands",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.AddColumn<string>(
                name: "TrackingNumber",
                table: "Commands",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Commands",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.CreateTable(
                name: "StoredImages",
                columns: table => new
                {
                    FileName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Data = table.Column<byte[]>(type: "bytea", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoredImages", x => x.FileName);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Commands_CreatedAt",
                table: "Commands",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Commands_PaymentReference",
                table: "Commands",
                column: "PaymentReference",
                unique: true,
                filter: "\"PaymentReference\" IS NOT NULL AND \"PaymentReference\" <> ''");

            migrationBuilder.CreateIndex(
                name: "IX_Commands_ReferenceNumber",
                table: "Commands",
                column: "ReferenceNumber",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StoredImages");

            migrationBuilder.DropIndex(
                name: "IX_Commands_CreatedAt",
                table: "Commands");

            migrationBuilder.DropIndex(
                name: "IX_Commands_PaymentReference",
                table: "Commands");

            migrationBuilder.DropIndex(
                name: "IX_Commands_ReferenceNumber",
                table: "Commands");

            migrationBuilder.DropColumn(
                name: "CancelledAt",
                table: "Commands");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Commands");

            migrationBuilder.DropColumn(
                name: "DeliveredAt",
                table: "Commands");

            migrationBuilder.DropColumn(
                name: "StatusNote",
                table: "Commands");

            migrationBuilder.DropColumn(
                name: "StatusUpdatedAt",
                table: "Commands");

            migrationBuilder.DropColumn(
                name: "TrackingNumber",
                table: "Commands");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Commands");
        }
    }
}
