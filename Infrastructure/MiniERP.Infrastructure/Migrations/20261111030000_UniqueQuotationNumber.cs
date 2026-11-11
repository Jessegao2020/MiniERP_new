using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MiniERP.Infrastructure.Data;

#nullable disable

namespace MiniERP.Infrastructure.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261111030000_UniqueQuotationNumber")]
public partial class UniqueQuotationNumber : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateIndex(
            name: "IX_Quotations_QuotationNumber",
            table: "Quotations",
            column: "QuotationNumber",
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_Quotations_QuotationNumber",
            table: "Quotations");
    }
}
