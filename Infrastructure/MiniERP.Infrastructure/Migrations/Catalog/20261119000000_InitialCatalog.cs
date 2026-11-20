using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MiniERP.Infrastructure.Data;

#nullable disable

namespace MiniERP.Infrastructure.Migrations.Catalog;

[DbContext(typeof(CatalogDbContext))]
[Migration("20261119000000_InitialCatalog")]
public sealed class InitialCatalog : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Articles",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false).Annotation("Sqlite:Autoincrement", true),
                Name = table.Column<string>(type: "TEXT", nullable: true),
                Price = table.Column<decimal>(type: "TEXT", nullable: true),
                MinimumPrice = table.Column<decimal>(type: "TEXT", nullable: true),
                Description = table.Column<string>(type: "TEXT", nullable: true),
                Specification = table.Column<string>(type: "TEXT", nullable: true),
                Discount = table.Column<string>(type: "TEXT", nullable: true),
                Note = table.Column<string>(type: "TEXT", nullable: true),
                Specs_EN = table.Column<string>(type: "TEXT", nullable: true),
                Category = table.Column<string>(type: "TEXT", nullable: true),
                Name_EN = table.Column<string>(type: "TEXT", nullable: true),
                Description_EN = table.Column<string>(type: "TEXT", nullable: true),
                CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                LastModifiedBy = table.Column<string>(type: "TEXT", nullable: true),
                LastModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
            },
            constraints: table => table.PrimaryKey("PK_Articles", x => x.Id));
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("Articles");
    }
}
