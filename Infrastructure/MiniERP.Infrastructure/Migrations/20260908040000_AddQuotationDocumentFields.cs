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
        migrationBuilder.AddColumn<string>(name: "CustomerNameSnapshot", table: "Quotations", type: "TEXT", nullable: true);
        migrationBuilder.AddColumn<string>(name: "CustomerAddressSnapshot", table: "Quotations", type: "TEXT", nullable: true);
        migrationBuilder.AddColumn<string>(name: "CustomerContactSnapshot", table: "Quotations", type: "TEXT", nullable: true);
        migrationBuilder.AddColumn<string>(name: "SalesContactNameSnapshot", table: "Quotations", type: "TEXT", nullable: true);
        migrationBuilder.AddColumn<string>(name: "SalesContactPhoneSnapshot", table: "Quotations", type: "TEXT", nullable: true);
        migrationBuilder.AddColumn<string>(name: "SalesContactEmailSnapshot", table: "Quotations", type: "TEXT", nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "Unit",
            table: "QuotationItems",
            type: "TEXT",
            nullable: false,
            defaultValue: "PCS");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "CustomerNameSnapshot", table: "Quotations");
        migrationBuilder.DropColumn(name: "CustomerAddressSnapshot", table: "Quotations");
        migrationBuilder.DropColumn(name: "CustomerContactSnapshot", table: "Quotations");
        migrationBuilder.DropColumn(name: "SalesContactNameSnapshot", table: "Quotations");
        migrationBuilder.DropColumn(name: "SalesContactPhoneSnapshot", table: "Quotations");
        migrationBuilder.DropColumn(name: "SalesContactEmailSnapshot", table: "Quotations");
        migrationBuilder.DropColumn(name: "Unit", table: "QuotationItems");
    }
}
