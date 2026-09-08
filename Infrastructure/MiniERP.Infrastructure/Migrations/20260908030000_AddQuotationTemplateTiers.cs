using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MiniERP.Infrastructure.Data;

#nullable disable

namespace MiniERP.Infrastructure.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260908030000_AddQuotationTemplateTiers")]
public partial class AddQuotationTemplateTiers : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
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

        migrationBuilder.AddColumn<string>(
            name: "CustomerNameSnapshot",
            table: "Quotations",
            type: "TEXT",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "CustomerAddressSnapshot",
            table: "Quotations",
            type: "TEXT",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "CustomerContactSnapshot",
            table: "Quotations",
            type: "TEXT",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "SalesContactNameSnapshot",
            table: "Quotations",
            type: "TEXT",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "SalesContactPhoneSnapshot",
            table: "Quotations",
            type: "TEXT",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "SalesContactEmailSnapshot",
            table: "Quotations",
            type: "TEXT",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "Unit",
            table: "QuotationItems",
            type: "TEXT",
            nullable: false,
            defaultValue: "PCS");

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

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "Tier1Label", table: "Quotations");
        migrationBuilder.DropColumn(name: "Tier2Label", table: "Quotations");
        migrationBuilder.DropColumn(name: "Tier3Label", table: "Quotations");
        migrationBuilder.DropColumn(name: "CustomerNameSnapshot", table: "Quotations");
        migrationBuilder.DropColumn(name: "CustomerAddressSnapshot", table: "Quotations");
        migrationBuilder.DropColumn(name: "CustomerContactSnapshot", table: "Quotations");
        migrationBuilder.DropColumn(name: "SalesContactNameSnapshot", table: "Quotations");
        migrationBuilder.DropColumn(name: "SalesContactPhoneSnapshot", table: "Quotations");
        migrationBuilder.DropColumn(name: "SalesContactEmailSnapshot", table: "Quotations");

        migrationBuilder.DropColumn(name: "Unit", table: "QuotationItems");
        migrationBuilder.DropColumn(name: "Quantity2", table: "QuotationItems");
        migrationBuilder.DropColumn(name: "UnitPrice2", table: "QuotationItems");
        migrationBuilder.DropColumn(name: "Quantity3", table: "QuotationItems");
        migrationBuilder.DropColumn(name: "UnitPrice3", table: "QuotationItems");
    }
}
