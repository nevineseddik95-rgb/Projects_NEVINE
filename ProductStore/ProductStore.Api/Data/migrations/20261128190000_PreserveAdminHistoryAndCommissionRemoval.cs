using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace productStore.Api.Data.migrations
{
    /// <inheritdoc />
    public partial class PreserveAdminHistoryAndCommissionRemoval : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Commands_AdminUsers_CreatedByAdminId",
                table: "Commands");

            migrationBuilder.AddColumn<DateTime>(
                name: "CommissionRemovedAt",
                table: "Commands",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedByAdminName",
                table: "Commands",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedByAdminPhone",
                table: "Commands",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedByAdminRole",
                table: "Commands",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Phone",
                table: "AdminUsers",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE "Commands" AS command
                SET "CreatedByAdminName" = admin."Username",
                    "CreatedByAdminRole" = admin."Role",
                    "CreatedByAdminPhone" = admin."Phone"
                FROM "AdminUsers" AS admin
                WHERE command."CreatedByAdminId" = admin."Id";
                """);

            migrationBuilder.AddForeignKey(
                name: "FK_Commands_AdminUsers_CreatedByAdminId",
                table: "Commands",
                column: "CreatedByAdminId",
                principalTable: "AdminUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Commands_AdminUsers_CreatedByAdminId",
                table: "Commands");

            migrationBuilder.DropColumn(
                name: "CommissionRemovedAt",
                table: "Commands");

            migrationBuilder.DropColumn(
                name: "CreatedByAdminName",
                table: "Commands");

            migrationBuilder.DropColumn(
                name: "CreatedByAdminPhone",
                table: "Commands");

            migrationBuilder.DropColumn(
                name: "CreatedByAdminRole",
                table: "Commands");

            migrationBuilder.DropColumn(
                name: "Phone",
                table: "AdminUsers");

            migrationBuilder.AddForeignKey(
                name: "FK_Commands_AdminUsers_CreatedByAdminId",
                table: "Commands",
                column: "CreatedByAdminId",
                principalTable: "AdminUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
