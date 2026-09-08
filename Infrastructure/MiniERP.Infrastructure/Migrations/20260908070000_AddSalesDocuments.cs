using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MiniERP.Infrastructure.Data;

#nullable disable

namespace MiniERP.Infrastructure.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260908070000_AddSalesDocuments")]
public partial class AddSalesDocuments : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Invoices",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false).Annotation("Sqlite:Autoincrement", true),
                Type = table.Column<int>(type: "INTEGER", nullable: false),
                InvoiceNumber = table.Column<string>(type: "TEXT", nullable: false),
                InvoiceDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                DueDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                CustomerId = table.Column<int>(type: "INTEGER", nullable: false),
                UserId = table.Column<int>(type: "INTEGER", nullable: false),
                Currency = table.Column<string>(type: "TEXT", nullable: false),
                ExchangeRate = table.Column<decimal>(type: "TEXT", nullable: false),
                DeliveryTerm = table.Column<string>(type: "TEXT", nullable: true),
                PaymentTerm = table.Column<string>(type: "TEXT", nullable: true),
                BankInformation = table.Column<string>(type: "TEXT", nullable: true),
                Remarks = table.Column<string>(type: "TEXT", nullable: true),
                CustomerPoNumber = table.Column<string>(type: "TEXT", nullable: true),
                CustomerPoDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                CustomerNameSnapshot = table.Column<string>(type: "TEXT", nullable: true),
                CustomerAddressSnapshot = table.Column<string>(type: "TEXT", nullable: true),
                CustomerContactSnapshot = table.Column<string>(type: "TEXT", nullable: true),
                SalesContactNameSnapshot = table.Column<string>(type: "TEXT", nullable: true),
                SalesContactPhoneSnapshot = table.Column<string>(type: "TEXT", nullable: true),
                SalesContactEmailSnapshot = table.Column<string>(type: "TEXT", nullable: true),
                SourceDocumentType = table.Column<int>(type: "INTEGER", nullable: false),
                SourceDocumentId = table.Column<int>(type: "INTEGER", nullable: true),
                SourceDocumentNumber = table.Column<string>(type: "TEXT", nullable: true),
                CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                LastModifiedBy = table.Column<string>(type: "TEXT", nullable: true),
                LastModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Invoices", x => x.Id);
                table.ForeignKey("FK_Invoices_Customers_CustomerId", x => x.CustomerId, "Customers", "Id", onDelete: ReferentialAction.Cascade);
                table.ForeignKey("FK_Invoices_Users_UserId", x => x.UserId, "Users", "Id", onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "PackingLists",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false).Annotation("Sqlite:Autoincrement", true),
                PackingListNumber = table.Column<string>(type: "TEXT", nullable: false),
                PackingDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                CustomerId = table.Column<int>(type: "INTEGER", nullable: false),
                UserId = table.Column<int>(type: "INTEGER", nullable: false),
                DeliveryTerm = table.Column<string>(type: "TEXT", nullable: true),
                Remarks = table.Column<string>(type: "TEXT", nullable: true),
                CustomerPoNumber = table.Column<string>(type: "TEXT", nullable: true),
                CustomerPoDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                CustomerNameSnapshot = table.Column<string>(type: "TEXT", nullable: true),
                CustomerAddressSnapshot = table.Column<string>(type: "TEXT", nullable: true),
                CustomerContactSnapshot = table.Column<string>(type: "TEXT", nullable: true),
                SalesContactNameSnapshot = table.Column<string>(type: "TEXT", nullable: true),
                SalesContactPhoneSnapshot = table.Column<string>(type: "TEXT", nullable: true),
                SalesContactEmailSnapshot = table.Column<string>(type: "TEXT", nullable: true),
                SourceDocumentType = table.Column<int>(type: "INTEGER", nullable: false),
                SourceDocumentId = table.Column<int>(type: "INTEGER", nullable: true),
                SourceDocumentNumber = table.Column<string>(type: "TEXT", nullable: true),
                CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                LastModifiedBy = table.Column<string>(type: "TEXT", nullable: true),
                LastModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PackingLists", x => x.Id);
                table.ForeignKey("FK_PackingLists_Customers_CustomerId", x => x.CustomerId, "Customers", "Id", onDelete: ReferentialAction.Cascade);
                table.ForeignKey("FK_PackingLists_Users_UserId", x => x.UserId, "Users", "Id", onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "InvoiceItems",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false).Annotation("Sqlite:Autoincrement", true),
                InvoiceId = table.Column<int>(type: "INTEGER", nullable: false),
                SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                SourceArticleId = table.Column<int>(type: "INTEGER", nullable: true),
                ArticleName = table.Column<string>(type: "TEXT", nullable: false),
                Description = table.Column<string>(type: "TEXT", nullable: true),
                Specification = table.Column<string>(type: "TEXT", nullable: true),
                Quantity = table.Column<decimal>(type: "TEXT", nullable: false),
                Unit = table.Column<string>(type: "TEXT", nullable: false),
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
                table.PrimaryKey("PK_InvoiceItems", x => x.Id);
                table.ForeignKey("FK_InvoiceItems_Invoices_InvoiceId", x => x.InvoiceId, "Invoices", "Id", onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "PackingListItems",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false).Annotation("Sqlite:Autoincrement", true),
                PackingListId = table.Column<int>(type: "INTEGER", nullable: false),
                SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                SourceArticleId = table.Column<int>(type: "INTEGER", nullable: true),
                ArticleName = table.Column<string>(type: "TEXT", nullable: false),
                Description = table.Column<string>(type: "TEXT", nullable: true),
                Specification = table.Column<string>(type: "TEXT", nullable: true),
                Quantity = table.Column<decimal>(type: "TEXT", nullable: false),
                Unit = table.Column<string>(type: "TEXT", nullable: false),
                CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                LastModifiedBy = table.Column<string>(type: "TEXT", nullable: true),
                LastModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PackingListItems", x => x.Id);
                table.ForeignKey("FK_PackingListItems_PackingLists_PackingListId", x => x.PackingListId, "PackingLists", "Id", onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "PackingPackages",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false).Annotation("Sqlite:Autoincrement", true),
                PackingListId = table.Column<int>(type: "INTEGER", nullable: false),
                SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                CartonNumber = table.Column<string>(type: "TEXT", nullable: false),
                PackageCount = table.Column<int>(type: "INTEGER", nullable: false),
                Contents = table.Column<string>(type: "TEXT", nullable: true),
                LengthCm = table.Column<decimal>(type: "TEXT", nullable: false),
                WidthCm = table.Column<decimal>(type: "TEXT", nullable: false),
                HeightCm = table.Column<decimal>(type: "TEXT", nullable: false),
                NetWeightKg = table.Column<decimal>(type: "TEXT", nullable: false),
                GrossWeightKg = table.Column<decimal>(type: "TEXT", nullable: false),
                CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                LastModifiedBy = table.Column<string>(type: "TEXT", nullable: true),
                LastModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PackingPackages", x => x.Id);
                table.ForeignKey("FK_PackingPackages_PackingLists_PackingListId", x => x.PackingListId, "PackingLists", "Id", onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex("IX_Invoices_CustomerId", "Invoices", "CustomerId");
        migrationBuilder.CreateIndex("IX_Invoices_UserId", "Invoices", "UserId");
        migrationBuilder.CreateIndex("IX_Invoices_InvoiceNumber", "Invoices", "InvoiceNumber", unique: true);
        migrationBuilder.CreateIndex("IX_InvoiceItems_InvoiceId", "InvoiceItems", "InvoiceId");
        migrationBuilder.CreateIndex("IX_PackingLists_CustomerId", "PackingLists", "CustomerId");
        migrationBuilder.CreateIndex("IX_PackingLists_UserId", "PackingLists", "UserId");
        migrationBuilder.CreateIndex("IX_PackingLists_PackingListNumber", "PackingLists", "PackingListNumber", unique: true);
        migrationBuilder.CreateIndex("IX_PackingListItems_PackingListId", "PackingListItems", "PackingListId");
        migrationBuilder.CreateIndex("IX_PackingPackages_PackingListId", "PackingPackages", "PackingListId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("InvoiceItems");
        migrationBuilder.DropTable("PackingListItems");
        migrationBuilder.DropTable("PackingPackages");
        migrationBuilder.DropTable("Invoices");
        migrationBuilder.DropTable("PackingLists");
    }
}
