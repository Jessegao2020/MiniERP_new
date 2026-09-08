using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MiniERP.Infrastructure.Data;

#nullable disable

namespace MiniERP.Infrastructure.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260908023000_AddQuotationItems")]
public partial class AddQuotationItems : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "Currency",
            table: "Quotations",
            type: "TEXT",
            nullable: false,
            defaultValue: "USD");

        migrationBuilder.AddColumn<decimal>(
            name: "ExchangeRate",
            table: "Quotations",
            type: "TEXT",
            nullable: false,
            defaultValue: 1m);

        migrationBuilder.CreateTable(
            name: "QuotationItems",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                QuotationId = table.Column<int>(type: "INTEGER", nullable: false),
                SourceArticleId = table.Column<int>(type: "INTEGER", nullable: true),
                ArticleName = table.Column<string>(type: "TEXT", nullable: false),
                Description = table.Column<string>(type: "TEXT", nullable: true),
                Specification = table.Column<string>(type: "TEXT", nullable: true),
                Quantity = table.Column<decimal>(type: "TEXT", nullable: false),
                UnitPrice = table.Column<decimal>(type: "TEXT", nullable: false),
                DiscountPercent = table.Column<decimal>(type: "TEXT", nullable: false),
                Currency = table.Column<string>(type: "TEXT", nullable: false),
                ExchangeRateSnapshot = table.Column<decimal>(type: "TEXT", nullable: false),
                CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                LastModifiedBy = table.Column<string>(type: "TEXT", nullable: true),
                LastModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_QuotationItems", x => x.Id);
                table.ForeignKey(
                    name: "FK_QuotationItems_Quotations_QuotationId",
                    column: x => x.QuotationId,
                    principalTable: "Quotations",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_QuotationItems_QuotationId",
            table: "QuotationItems",
            column: "QuotationId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "QuotationItems");
        migrationBuilder.DropColumn(name: "Currency", table: "Quotations");
        migrationBuilder.DropColumn(name: "ExchangeRate", table: "Quotations");
    }
}
