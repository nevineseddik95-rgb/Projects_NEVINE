using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace productStore.Api.Data.migrations;

/// <inheritdoc />
public partial class _20261121220222_InitialCreate : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Colors",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                Name = table.Column<string>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Colors", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "Commands",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                ReferenceNumber = table.Column<string>(type: "TEXT", nullable: false),
                CustomerId = table.Column<Guid>(type: "TEXT", nullable: false),
                ProductId = table.Column<Guid>(type: "TEXT", nullable: false),
                Model = table.Column<string>(type: "TEXT", nullable: false),
                GenreId = table.Column<Guid>(type: "TEXT", nullable: false),
                ColorId = table.Column<Guid>(type: "TEXT", nullable: false),
                SizeId = table.Column<Guid>(type: "TEXT", nullable: true),
                DeliveryAddress = table.Column<string>(type: "TEXT", nullable: false),
                Date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                Status = table.Column<string>(type: "TEXT", nullable: false),
                Total = table.Column<decimal>(type: "TEXT", nullable: false),
                Note = table.Column<string>(type: "TEXT", nullable: true),
                ImageUrl = table.Column<string>(type: "TEXT", nullable: true),
                DepositAmount = table.Column<decimal>(type: "TEXT", nullable: false),
                PaymentStatus = table.Column<string>(type: "TEXT", nullable: false),
                PaymentReference = table.Column<string>(type: "TEXT", nullable: true),
                PaymentMethod = table.Column<string>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Commands", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "Customers",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                FullName = table.Column<string>(type: "TEXT", nullable: false),
                Email = table.Column<string>(type: "TEXT", nullable: false),
                Phone = table.Column<string>(type: "TEXT", nullable: true),
                Address = table.Column<string>(type: "TEXT", nullable: true),
                LastVisit = table.Column<DateTime>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Customers", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "Genres",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                Name = table.Column<string>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Genres", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "Sizes",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                Name = table.Column<string>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Sizes", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "Products",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                Model = table.Column<string>(type: "TEXT", nullable: false),
                GenreId = table.Column<Guid>(type: "TEXT", nullable: false),
                ColorId = table.Column<Guid>(type: "TEXT", nullable: false),
                SizeId = table.Column<Guid>(type: "TEXT", nullable: false),
                Price = table.Column<decimal>(type: "TEXT", nullable: false),
                ReleaseDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                ImageUrl = table.Column<string>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Products", x => x.Id);
                table.ForeignKey(
                    name: "FK_Products_Colors_ColorId",
                    column: x => x.ColorId,
                    principalTable: "Colors",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_Products_Genres_GenreId",
                    column: x => x.GenreId,
                    principalTable: "Genres",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_Products_Sizes_SizeId",
                    column: x => x.SizeId,
                    principalTable: "Sizes",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Products_ColorId",
            table: "Products",
            column: "ColorId");

        migrationBuilder.CreateIndex(
            name: "IX_Products_GenreId",
            table: "Products",
            column: "GenreId");

        migrationBuilder.CreateIndex(
            name: "IX_Products_SizeId",
            table: "Products",
            column: "SizeId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "Commands");

        migrationBuilder.DropTable(
            name: "Customers");

        migrationBuilder.DropTable(
            name: "Products");

        migrationBuilder.DropTable(
            name: "Colors");

        migrationBuilder.DropTable(
            name: "Genres");

        migrationBuilder.DropTable(
            name: "Sizes");
    }
}
