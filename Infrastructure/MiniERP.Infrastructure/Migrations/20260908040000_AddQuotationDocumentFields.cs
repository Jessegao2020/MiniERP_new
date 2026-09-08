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
        // Compatibility marker only.
        //
        // 20260908030000_AddQuotationTemplateTiers was briefly published and may
        // already be recorded in user databases. It introduced the document snapshot
        // fields and QuotationItem.Unit that the current single-tier PDF design needs,
        // along with several abandoned tier columns.
        //
        // Do not add the required columns again: that caused duplicate-column errors
        // for databases which had already run 030000. Also do not DropColumn here:
        // EF Core's SQLite migrations provider does not support DropColumnOperation.
        // The obsolete physical columns are harmless because the current EF model does
        // not map them. Keeping them is safer than rebuilding tables and preserves data.
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // No-op for the same compatibility reason. The schema additions belong to
        // migration 030000 and must remain owned by that published migration.
    }
}
