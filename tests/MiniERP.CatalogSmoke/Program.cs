using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MiniERP.Domain;
using MiniERP.Infrastructure.Data;

var folder = Path.Combine(Path.GetTempPath(), "minierp-catalog-smoke-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(folder);
var erpPath = Path.Combine(folder, "erp.db");
var catalogPath = Path.Combine(folder, "catalog.db");
var backupPath = Path.Combine(folder, "erp.pre-catalog-split.db");
var erpOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
    .UseSqlite($"Data Source={erpPath}").Options;
var catalogOptions = new DbContextOptionsBuilder<CatalogDbContext>()
    .UseSqlite($"Data Source={catalogPath}").Options;

static void Check(bool ok, string description)
{
    if (!ok) throw new Exception("FAILED: " + description);
    Console.WriteLine("PASS: " + description);
}

try
{
    // Seed a disposable legacy database with both shared and confidential data.
    using (var erp = new ApplicationDbContext(erpOptions))
    {
        erp.Database.Migrate();
        erp.Articles.Add(new Article
        {
            Id = 27, Name = "FET3588-C", Name_EN = "Test module",
            Price = 99.1234m, MinimumPrice = 75.50m, Discount = "5",
            Category = "SoM", Note = "Keep every field",
            Description_EN = "Detailed description",
            CreatedBy = "test", CreatedAt = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc)
        });
        erp.Customers.Add(new Customer { Name = "CONFIDENTIAL-TEST-CUSTOMER" });
        erp.SaveChanges();
    }

    CatalogDatabaseBootstrapper.BackupLegacyDatabaseBeforeFirstSplit(erpPath, catalogPath);
    Check(File.Exists(backupPath), "First upgrade creates a private ERP backup");

    using (var catalog = new CatalogDbContext(catalogOptions))
        catalog.Database.Migrate();
    CatalogDatabaseBootstrapper.Initialize(erpPath, catalogPath);

    using (var catalog = new CatalogDbContext(catalogOptions))
    {
        var rows = catalog.Articles.AsNoTracking().ToList();
        Check(rows.Count == 1 && rows[0].Id == 27, "Article IDs preserved exactly");
        Check(rows[0].Price == 99.1234m && rows[0].MinimumPrice == 75.50m &&
              rows[0].Description_EN == "Detailed description" &&
              rows[0].CreatedBy == "test" &&
              rows[0].CreatedAt == new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc),
              "Values and audit fields preserved");
    }

    using (var sql = new SqliteConnection($"Data Source={catalogPath}"))
    {
        sql.Open();
        using var cmd = sql.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' " +
                          "AND name IN ('Customers','Contacts','Quotations','Invoices','Contracts','PackingLists','Users')";
        Check(Convert.ToInt64(cmd.ExecuteScalar()) == 0,
              "catalog.db has no personal customer or business-document tables");
    }

    using (var erp = new ApplicationDbContext(erpOptions))
    {
        Check(erp.Customers.Count() == 1 &&
              erp.Customers.Single().Name == "CONFIDENTIAL-TEST-CUSTOMER",
              "Existing customer survives migration unchanged");
        Check(erp.Articles.Single().Id == 27,
              "Original ERP Articles remain intact for rollback");
    }

    CatalogDatabaseBootstrapper.BackupLegacyDatabaseBeforeFirstSplit(erpPath, catalogPath);
    CatalogDatabaseBootstrapper.Initialize(erpPath, catalogPath);
    using (var catalog = new CatalogDbContext(catalogOptions))
    {
        Check(catalog.Articles.Count() == 1, "Restart does not duplicate imported Articles");
        catalog.Articles.Add(new Article { Name = "NEW-CATALOG-ONLY" });
        catalog.SaveChanges();
        Check(catalog.Articles.Single(a => a.Name == "NEW-CATALOG-ONLY").Id > 27,
              "New Article IDs continue after imported IDs");
    }

    using (var erp = new ApplicationDbContext(erpOptions))
        Check(erp.Articles.Count() == 1 && erp.Customers.Count() == 1,
              "New catalogue writes never touch legacy ERP tables");

    using (var backup = new SqliteConnection($"Data Source={backupPath}"))
    {
        backup.Open();
        using var cmd = backup.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM Customers WHERE Name='CONFIDENTIAL-TEST-CUSTOMER'";
        Check(Convert.ToInt64(cmd.ExecuteScalar()) == 1, "Backup contains intact customer data");
    }

    Console.WriteLine("Catalog split smoke test passed.");
}
finally
{
    Directory.Delete(folder, recursive: true);
}
