using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace productStore.Api.Data.migrations
{
    /// <inheritdoc />
    public partial class AddCommissionPayoutTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "CommissionPaidAmount",
                table: "Commands",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "CommissionPaidAt",
                table: "Commands",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CommissionPaymentNote",
                table: "Commands",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CommissionPaidAmount",
                table: "Commands");

            migrationBuilder.DropColumn(
                name: "CommissionPaidAt",
                table: "Commands");

            migrationBuilder.DropColumn(
                name: "CommissionPaymentNote",
                table: "Commands");
        }
    }
}
