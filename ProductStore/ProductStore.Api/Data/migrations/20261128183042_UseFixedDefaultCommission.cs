using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace productStore.Api.Data.migrations
{
    /// <inheritdoc />
    public partial class UseFixedDefaultCommission : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE "Commands" AS command
                SET "CommissionAmount" = GREATEST(500, command."CommissionPaidAmount")
                FROM "AdminUsers" AS admin
                WHERE command."CreatedByAdminId" = admin."Id"
                  AND admin."Role" <> 'Owner';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE "Commands" AS command
                SET "CommissionAmount" = GREATEST(ROUND(command."Total" * 0.05, 2), command."CommissionPaidAmount")
                FROM "AdminUsers" AS admin
                WHERE command."CreatedByAdminId" = admin."Id"
                  AND admin."Role" <> 'Owner';
                """);
        }
    }
}
