using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace productStore.Api.Data.migrations
{
    /// <inheritdoc />
    public partial class AddAdminOrderCommissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "CommissionAmount",
                table: "Commands",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedByAdminId",
                table: "Commands",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Commands_CreatedByAdminId",
                table: "Commands",
                column: "CreatedByAdminId");

            migrationBuilder.AddForeignKey(
                name: "FK_Commands_AdminUsers_CreatedByAdminId",
                table: "Commands",
                column: "CreatedByAdminId",
                principalTable: "AdminUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Commands_AdminUsers_CreatedByAdminId",
                table: "Commands");

            migrationBuilder.DropIndex(
                name: "IX_Commands_CreatedByAdminId",
                table: "Commands");

            migrationBuilder.DropColumn(
                name: "CommissionAmount",
                table: "Commands");

            migrationBuilder.DropColumn(
                name: "CreatedByAdminId",
                table: "Commands");
        }
    }
}
