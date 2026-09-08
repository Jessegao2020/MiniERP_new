using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MiniERP.Infrastructure.Data;

#nullable disable

namespace MiniERP.Infrastructure.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260908080000_AddContracts")]
public partial class AddContracts : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Contracts",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false).Annotation("Sqlite:Autoincrement", true),
                ContractNumber = table.Column<string>(type: "TEXT", nullable: false),
                ContractDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                CustomerId = table.Column<int>(type: "INTEGER", nullable: false),
                UserId = table.Column<int>(type: "INTEGER", nullable: false),
                Currency = table.Column<string>(type: "TEXT", nullable: false),
                ExchangeRate = table.Column<decimal>(type: "TEXT", nullable: false),
                DeliveryTerm = table.Column<string>(type: "TEXT", nullable: true),
                PaymentTerm = table.Column<string>(type: "TEXT", nullable: true),
                LeadTime = table.Column<string>(type: "TEXT", nullable: true),
                BankInformation = table.Column<string>(type: "TEXT", nullable: true),
                Remarks = table.Column<string>(type: "TEXT", nullable: true),
                TermsAndConditions = table.Column<string>(type: "TEXT", nullable: true),
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
                table.PrimaryKey("PK_Contracts", x => x.Id);
                table.ForeignKey("FK_Contracts_Customers_CustomerId", x => x.CustomerId, "Customers", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_Contracts_Users_UserId", x => x.UserId, "Users", "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "ContractItems",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false).Annotation("Sqlite:Autoincrement", true),
                ContractId = table.Column<int>(type: "INTEGER", nullable: false),
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
                table.PrimaryKey("PK_ContractItems", x => x.Id);
                table.ForeignKey("FK_ContractItems_Contracts_ContractId", x => x.ContractId, "Contracts", "Id", onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex("IX_Contracts_ContractNumber", "Contracts", "ContractNumber", unique: true);
        migrationBuilder.CreateIndex("IX_Contracts_CustomerId", "Contracts", "CustomerId");
        migrationBuilder.CreateIndex("IX_Contracts_UserId", "Contracts", "UserId");
        migrationBuilder.CreateIndex("IX_ContractItems_ContractId", "ContractItems", "ContractId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("ContractItems");
        migrationBuilder.DropTable("Contracts");
    }
}
