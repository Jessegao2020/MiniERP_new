using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MiniERP.Infrastructure.Data;

#nullable disable

namespace MiniERP.Infrastructure.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260908040000_AddQuotationDocumentFields")]
public partial class AddQuotationDocumentFields : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // 20260908030000 was briefly published and may already exist in user databases.
        // Keep that migration in the chain forever, then remove only the fields that
        // belonged to the abandoned three-tier quotation design. The document snapshot
        // fields and Unit column introduced by 030000 are intentionally retained.
        migrationBuilder.DropColumn(name: "Tier1Label", table: "Quotations");
        migrationBuilder.DropColumn(name: "Tier2Label", table: "Quotations");
        migrationBuilder.DropColumn(name: "Tier3Label", table: "Quotations");

        migrationBuilder.DropColumn(name: "Quantity2", table: "QuotationItems");
        migrationBuilder.DropColumn(name: "UnitPrice2", table: "QuotationItems");
        migrationBuilder.DropColumn(name: "Quantity3", table: "QuotationItems");
        migrationBuilder.DropColumn(name: "UnitPrice3", table: "QuotationItems");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "Tier1Label",
            table: "Quotations",
            type: "TEXT",
            nullable: false,
            defaultValue: "100 Sets");

        migrationBuilder.AddColumn<string>(
            name: "Tier2Label",
            table: "Quotations",
            type: "TEXT",
            nullable: false,
            defaultValue: "500 Sets");

        migrationBuilder.AddColumn<string>(
            name: "Tier3Label",
            table: "Quotations",
            type: "TEXT",
            nullable: false,
            defaultValue: "1000 Sets");

        migrationBuilder.AddColumn<decimal>(
            name: "Quantity2",
            table: "QuotationItems",
            type: "TEXT",
            nullable: false,
            defaultValue: 0m);

        migrationBuilder.AddColumn<decimal>(
            name: "UnitPrice2",
            table: "QuotationItems",
            type: "TEXT",
            nullable: false,
            defaultValue: 0m);

        migrationBuilder.AddColumn<decimal>(
            name: "Quantity3",
            table: "QuotationItems",
            type: "TEXT",
            nullable: false,
            defaultValue: 0m);

        migrationBuilder.AddColumn<decimal>(
            name: "UnitPrice3",
            table: "QuotationItems",
            type: "TEXT",
            nullable: false,
            defaultValue: 0m);

        migrationBuilder.Sql("""
            UPDATE QuotationItems
            SET Quantity2 = Quantity,
                UnitPrice2 = UnitPrice,
                Quantity3 = Quantity,
                UnitPrice3 = UnitPrice;
            """);
    }
}
