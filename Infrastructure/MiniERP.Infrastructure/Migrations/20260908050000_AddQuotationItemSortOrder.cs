using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MiniERP.Infrastructure.Data;

#nullable disable

namespace MiniERP.Infrastructure.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260908050000_AddQuotationItemSortOrder")]
public partial class AddQuotationItemSortOrder : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "SortOrder",
            table: "QuotationItems",
            type: "INTEGER",
            nullable: false,
            defaultValue: 0);

        // Preserve the historical visual order for quotations that already exist.
        // Items were previously displayed by primary key, so use that same order as
        // the initial persistent SortOrder within each quotation.
        migrationBuilder.Sql("""
            UPDATE QuotationItems
            SET SortOrder = (
                SELECT COUNT(*) - 1
                FROM QuotationItems AS earlier
                WHERE earlier.QuotationId = QuotationItems.QuotationId
                  AND earlier.Id <= QuotationItems.Id
            );
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Forward-only compatibility migration. Existing released SQLite databases
        // should not be rebuilt merely to remove this harmless ordering column.
    }
}
