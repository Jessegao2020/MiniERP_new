using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MiniERP.Infrastructure.Data;

#nullable disable

namespace MiniERP.Infrastructure.Migrations;

/// <summary>
/// Final physical split: Article records now live in catalog.db.
/// The application creates __CatalogSplitPermit only after validating the
/// separate catalogue and local backups. Direct ef database update without
/// the permit will fail before any destructive operation.
/// </summary>
[DbContext(typeof(ApplicationDbContext))]
[Migration("20261119093000_RemoveLegacyArticles")]
public sealed class RemoveLegacyArticles : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE TEMP TABLE "__MiniErpDropGuard"
                ("Ok" INTEGER NOT NULL CHECK ("Ok" = 1));
            INSERT INTO temp."__MiniErpDropGuard" ("Ok")
                SELECT CASE WHEN EXISTS
                    (SELECT 1 FROM main."__CatalogSplitPermit" WHERE "Version" = 1)
                    THEN 1 ELSE 0 END;
            DROP TABLE temp."__MiniErpDropGuard";
            """);

        migrationBuilder.DropTable(name: "Articles");
        migrationBuilder.Sql("DROP TABLE IF EXISTS __CatalogSplitPermit;");
    }

    // Downgrading recreates only the old schema, not deleted data.
    // Recover historical Article rows from the independent backups if necessary.
    protected override void Down(MigrationBuilder migrationBuilder)
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
}
