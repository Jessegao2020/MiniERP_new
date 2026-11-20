using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MiniERP.Domain;
using MiniERP.Infrastructure.Data;

var folder = Path.Combine(Path.GetTempPath(), "minierp-catalog-smoke-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(folder);

static void Check(bool ok, string description)
{
    if (!ok) throw new Exception("FAILED: " + description);
    Console.WriteLine("PASS: " + description);
}

static long SqlCount(string path, string sql)
{
    using var db = new SqliteConnection($"Data Source={path}");
    db.Open();
    using var command = db.CreateCommand();
    command.CommandText = sql;
    return Convert.ToInt64(command.ExecuteScalar());
}

static string SqlText(string path, string sql)
{
    using var db = new SqliteConnection($"Data Source={path}");
    db.Open();
    using var command = db.CreateCommand();
    command.CommandText = sql;
    return Convert.ToString(command.ExecuteScalar()) ?? string.Empty;
}

try
{
    var erpPath = Path.Combine(folder, "erp.db");
    var catalogPath = Path.Combine(folder, "catalog.db");
    var backupPath = Path.Combine(folder, "erp.pre-catalog-split.db");
    var erpOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseSqlite($"Data Source={erpPath}").Options;
    var catalogOptions = new DbContextOptionsBuilder<CatalogDbContext>()
        .UseSqlite($"Data Source={catalogPath}").Options;

    // Simulate an ERP from the previous release (before removal of Articles).
    using (var erp = new ApplicationDbContext(erpOptions))
    {
        erp.Database.GetService<IMigrator>().Migrate(DatabaseStartupMigrator.LastLegacyMigration);
        erp.Database.ExecuteSqlRaw("""
            INSERT INTO Articles (Id, Name, Name_EN, Price, MinimumPrice,
                                  Discount, Category, Note, Description_EN,
                                  CreatedBy, CreatedAt)
            VALUES (27, 'FET3588-C', 'Test module', '99.1234', '75.50', '5',
                    'SoM', 'Keep every field', 'Detailed description',
                    'test', '2026-01-02 03:04:05');
            """);
        erp.Customers.Add(new Customer { Name = "CONFIDENTIAL-TEST-CUSTOMER" });
        erp.SaveChanges();
    }

    // Direct EF update must be rejected: the catalogue is not backed up/imported.
    var blocked = false;
    try
    {
        using var erp = new ApplicationDbContext(erpOptions);
        erp.Database.Migrate();
    }
    catch (SqliteException)
    {
        blocked = true;
    }

    Check(blocked, "Direct EF update cannot drop Articles before catalogue verification");
    Check(SqlCount(erpPath,
        "SELECT COUNT(*) FROM sqlite_master WHERE name='Articles' AND type='table'") == 1,
        "Rejected unsafe migration leaves old Articles intact");

    // Reproduce a Phase 1 machine which already has a separate live catalogue.
    CatalogDatabaseBootstrapper.BackupLegacyDatabaseBeforeFirstSplit(erpPath, catalogPath);
    Check(File.Exists(backupPath), "Backup of historical customer data created");
    using (var catalog = new CatalogDbContext(catalogOptions))
        catalog.Database.Migrate();
    CatalogDatabaseBootstrapper.Initialize(erpPath, catalogPath);

    using (var catalog = new CatalogDbContext(catalogOptions))
    {
        var article = catalog.Articles.Single();
        Check(article.Id == 27 && article.Price == 99.1234m &&
              article.MinimumPrice == 75.50m && article.Name_EN == "Test module" &&
              article.CreatedBy == "test",
              "Imported Article fields, prices and IDs are preserved");
    }

    using (var erp = new ApplicationDbContext(erpOptions))
    using (var catalog = new CatalogDbContext(catalogOptions))
        DatabaseStartupMigrator.Upgrade(erp, catalog, erpPath, catalogPath);

    Check(File.Exists(Path.Combine(folder, "catalog.pre-legacy-drop.db")),
          "Active catalog receives an independent pre-cleanup backup");
    Check(SqlCount(erpPath,
        "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='Articles'") == 0,
        "Final ERP physically contains no Articles table");
    Check(SqlCount(erpPath,
        "SELECT COUNT(*) FROM __EFMigrationsHistory WHERE MigrationId='20261119093000_RemoveLegacyArticles'") == 1,
        "EF tracks the one-time cleanup migration");
    Check(SqlCount(erpPath,
        "SELECT COUNT(*) FROM Customers WHERE Name='CONFIDENTIAL-TEST-CUSTOMER'") == 1,
        "Historical customer remains untouched");
    Check(SqlCount(catalogPath,
        "SELECT COUNT(*) FROM Articles WHERE Id=27") == 1,
        "Original catalogue Article remains intact");
    Check(SqlCount(catalogPath,
        "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name IN " +
        "('Customers','Contacts','Quotations','Invoices','Contracts','PackingLists','Users')") == 0,
        "Catalogue has no personal/business tables");

    using (var erp = new ApplicationDbContext(erpOptions))
    using (var catalog = new CatalogDbContext(catalogOptions))
        DatabaseStartupMigrator.Upgrade(erp, catalog, erpPath, catalogPath);
    Check(SqlCount(erpPath,
        "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='Articles'") == 0,
        "Restart does not recreate old Articles table");

    using (var catalog = new CatalogDbContext(catalogOptions))
    {
        catalog.Articles.Add(new Article { Name = "NEW-CATALOG-ONLY" });
        catalog.SaveChanges();
        Check(catalog.Articles.Single(a => a.Name == "NEW-CATALOG-ONLY").Id > 27,
              "New Article stays in catalog and IDs continue");
    }
    Check(SqlCount(catalogPath, "SELECT COUNT(*) FROM Articles") == 2,
          "Catalog remains writable after ERP cleanup");
    Check(SqlCount(backupPath, "SELECT COUNT(*) FROM Articles WHERE Id=27") == 1 &&
          SqlCount(backupPath,
              "SELECT COUNT(*) FROM Customers WHERE Name='CONFIDENTIAL-TEST-CUSTOMER'") == 1,
          "Original ERP backup preserves legacy Articles and customers");

    // Also test a colleague's virgin installation with only a supplied catalog.db.
    var colleague = Path.Combine(folder, "colleague");
    Directory.CreateDirectory(colleague);
    var colleagueErp = Path.Combine(colleague, "erp.db");
    var colleagueCatalog = Path.Combine(colleague, "catalog.db");
    // SQLite may still have committed pages in the WAL; a byte-for-byte
    // File.Copy of the main .db alone is NOT necessarily a complete snapshot.
    using (var source = new SqliteConnection($"Data Source={catalogPath};Mode=ReadOnly"))
    using (var destination = new SqliteConnection($"Data Source={colleagueCatalog}"))
    {
        source.Open();
        destination.Open();
        source.BackupDatabase(destination);
    }
    var colleagueErpOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseSqlite($"Data Source={colleagueErp}").Options;
    var colleagueCatalogOptions = new DbContextOptionsBuilder<CatalogDbContext>()
        .UseSqlite($"Data Source={colleagueCatalog}").Options;
    using (var erp = new ApplicationDbContext(colleagueErpOptions))
    using (var catalog = new CatalogDbContext(colleagueCatalogOptions))
        DatabaseStartupMigrator.Upgrade(erp, catalog, colleagueErp, colleagueCatalog);

    Check(SqlCount(colleagueErp,
        "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='Articles'") == 0,
        "Colleague's new erp.db contains no redundant Articles");
    Check(SqlCount(colleagueErp, "SELECT COUNT(*) FROM Customers") == 0,
        "Colleague's ERP starts with no private customer records");
    Check(SqlCount(colleagueCatalog, "SELECT COUNT(*) FROM Articles") == 2,
        "Colleague can access the shared standalone catalogue");

    // An entirely fresh installation should also create only one Article table.
    var fresh = Path.Combine(folder, "fresh");
    Directory.CreateDirectory(fresh);
    var freshErp = Path.Combine(fresh, "erp.db");
    var freshCatalog = Path.Combine(fresh, "catalog.db");
    var freshErpOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseSqlite($"Data Source={freshErp}").Options;
    var freshCatalogOptions = new DbContextOptionsBuilder<CatalogDbContext>()
        .UseSqlite($"Data Source={freshCatalog}").Options;
    using (var erp = new ApplicationDbContext(freshErpOptions))
    using (var catalog = new CatalogDbContext(freshCatalogOptions))
        DatabaseStartupMigrator.Upgrade(erp, catalog, freshErp, freshCatalog);

    Check(SqlCount(freshErp,
        "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='Articles'") == 0,
        "Fresh installation erp.db has no Articles");
    Check(SqlCount(freshCatalog, "SELECT COUNT(*) FROM Articles") == 0,
        "Fresh installation starts with empty standalone catalog");
    Check(SqlText(freshErp, "PRAGMA integrity_check") == "ok" &&
          SqlText(freshCatalog, "PRAGMA integrity_check") == "ok",
          "Both fresh databases pass integrity verification");

    Console.WriteLine("All catalogue cleanup smoke tests passed.");
}
finally
{
    // Windows keeps pooled SQLite handles open after disposal unless explicitly cleared.
    SqliteConnection.ClearAllPools();
    Directory.Delete(folder, recursive: true);
}
