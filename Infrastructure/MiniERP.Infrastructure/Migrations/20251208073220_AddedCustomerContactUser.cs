using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddedCustomerContactUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Quotations_Customers_CustomerId",
                table: "Quotations");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Quotations",
                table: "Quotations");

            migrationBuilder.DropIndex(
                name: "IX_Quotations_QuotationNumber",
                table: "Quotations");

            migrationBuilder.DropIndex(
                name: "IX_Customers_Code",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Quotations");

            migrationBuilder.DropColumn(
                name: "Code",
                table: "Customers");

            migrationBuilder.RenameColumn(
                name: "TotalAmount",
                table: "Quotations",
                newName: "UserId");

            migrationBuilder.RenameColumn(
                name: "UpdatedAt",
                table: "Customers",
                newName: "State");

            migrationBuilder.RenameColumn(
                name: "Phone",
                table: "Customers",
                newName: "PostalCode");

            migrationBuilder.RenameColumn(
                name: "Email",
                table: "Customers",
                newName: "LastModifiedByUserName");

            migrationBuilder.RenameColumn(
                name: "ContactPerson",
                table: "Customers",
                newName: "LastModifiedAt");

            migrationBuilder.RenameColumn(
                name: "Address",
                table: "Customers",
                newName: "CreatedByUserName");

            migrationBuilder.AlterColumn<int>(
                name: "Id",
                table: "Quotations",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER")
                .OldAnnotation("Sqlite:Autoincrement", true);

            migrationBuilder.AddColumn<string>(
                name: "CratedBy",
                table: "Quotations",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeliveryTerm",
                table: "Quotations",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LeadTime",
                table: "Quotations",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentTerm",
                table: "Quotations",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UserId1",
                table: "Quotations",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AddressLine1",
                table: "Customers",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AddressLine2",
                table: "Customers",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "City",
                table: "Customers",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Country",
                table: "Customers",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "Articles",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "CreatedByUserName",
                table: "Articles",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastModifiedAt",
                table: "Articles",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastModifiedByUserName",
                table: "Articles",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_Quotations",
                table: "Quotations",
                column: "QuotationNumber");

            migrationBuilder.CreateTable(
                name: "Contacts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Title = table.Column<string>(type: "TEXT", nullable: true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    CustomerId = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedByUserName = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastModifiedByUserName = table.Column<string>(type: "TEXT", nullable: true),
                    LastModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Contacts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Contacts_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Code = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Email = table.Column<string>(type: "TEXT", nullable: false),
                    Phone = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Quotations_UserId1",
                table: "Quotations",
                column: "UserId1");

            migrationBuilder.CreateIndex(
                name: "IX_Contacts_CustomerId",
                table: "Contacts",
                column: "CustomerId");

            migrationBuilder.AddForeignKey(
                name: "FK_Quotations_Customers_CustomerId",
                table: "Quotations",
                column: "CustomerId",
                principalTable: "Customers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Quotations_Users_UserId1",
                table: "Quotations",
                column: "UserId1",
                principalTable: "Users",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Quotations_Customers_CustomerId",
                table: "Quotations");

            migrationBuilder.DropForeignKey(
                name: "FK_Quotations_Users_UserId1",
                table: "Quotations");

            migrationBuilder.DropTable(
                name: "Contacts");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Quotations",
                table: "Quotations");

            migrationBuilder.DropIndex(
                name: "IX_Quotations_UserId1",
                table: "Quotations");

            migrationBuilder.DropColumn(
                name: "CratedBy",
                table: "Quotations");

            migrationBuilder.DropColumn(
                name: "DeliveryTerm",
                table: "Quotations");

            migrationBuilder.DropColumn(
                name: "LeadTime",
                table: "Quotations");

            migrationBuilder.DropColumn(
                name: "PaymentTerm",
                table: "Quotations");

            migrationBuilder.DropColumn(
                name: "UserId1",
                table: "Quotations");

            migrationBuilder.DropColumn(
                name: "AddressLine1",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "AddressLine2",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "City",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "Country",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Articles");

            migrationBuilder.DropColumn(
                name: "CreatedByUserName",
                table: "Articles");

            migrationBuilder.DropColumn(
                name: "LastModifiedAt",
                table: "Articles");

            migrationBuilder.DropColumn(
                name: "LastModifiedByUserName",
                table: "Articles");

            migrationBuilder.RenameColumn(
                name: "UserId",
                table: "Quotations",
                newName: "TotalAmount");

            migrationBuilder.RenameColumn(
                name: "State",
                table: "Customers",
                newName: "UpdatedAt");

            migrationBuilder.RenameColumn(
                name: "PostalCode",
                table: "Customers",
                newName: "Phone");

            migrationBuilder.RenameColumn(
                name: "LastModifiedByUserName",
                table: "Customers",
                newName: "Email");

            migrationBuilder.RenameColumn(
                name: "LastModifiedAt",
                table: "Customers",
                newName: "ContactPerson");

            migrationBuilder.RenameColumn(
                name: "CreatedByUserName",
                table: "Customers",
                newName: "Address");

            migrationBuilder.AlterColumn<int>(
                name: "Id",
                table: "Quotations",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER")
                .Annotation("Sqlite:Autoincrement", true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "Quotations",
                type: "TEXT",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Code",
                table: "Customers",
                type: "TEXT",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Quotations",
                table: "Quotations",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_Quotations_QuotationNumber",
                table: "Quotations",
                column: "QuotationNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Customers_Code",
                table: "Customers",
                column: "Code",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Quotations_Customers_CustomerId",
                table: "Quotations",
                column: "CustomerId",
                principalTable: "Customers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
