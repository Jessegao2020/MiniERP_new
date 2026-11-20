using System.Globalization;
using Microsoft.Data.Sqlite;

namespace MiniERP.Infrastructure.Data;

/// <summary>
/// Copies historical Articles into the new catalog without deleting or modifying any
/// business record in erp.db. It is safe to resume after a failed first launch.
/// Run before the ERP migration that drops the historical Articles table.
/// </summary>
public static class CatalogDatabaseBootstrapper
{
    private static readonly string[] ArticleColumns =
    {
        "Id", "Name", "Price", "MinimumPrice", "Description", "Specification",
        "Discount", "Note", "Specs_EN", "Category", "Name_EN", "Description_EN",
        "CreatedBy", "CreatedAt", "LastModifiedBy", "LastModifiedAt"
    };

    /// <summary>
    /// Takes a consistent SQLite backup before any migrations on an existing erp.db.
    /// An existing backup is NEVER overwritten.
    /// </summary>
    public static void BackupLegacyDatabaseBeforeFirstSplit(string erpPath, string catalogPath)
    {
        if (!File.Exists(erpPath))
            return;

        var backupPath = Path.Combine(Path.GetDirectoryName(erpPath)!, "erp.pre-catalog-split.db");
        if (File.Exists(backupPath))
            return;

        var temporaryPath = backupPath + ".tmp";
        if (File.Exists(temporaryPath))
            File.Delete(temporaryPath);

        try
        {
            using (var source = new SqliteConnection(
                new SqliteConnectionStringBuilder { DataSource = erpPath, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString()))
            using (var destination = new SqliteConnection(
                new SqliteConnectionStringBuilder { DataSource = temporaryPath, Mode = SqliteOpenMode.ReadWriteCreate, Pooling = false }.ToString()))
            {
                source.Open();
                destination.Open();
                source.BackupDatabase(destination);
            }

            // The backup contains confidential customer records. Do not expose it to other users.
            if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
                File.SetUnixFileMode(temporaryPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);

            File.Move(temporaryPath, backupPath);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }

    public static void Initialize(string erpPath, string catalogPath)
    {
        if (Path.GetFullPath(erpPath) == Path.GetFullPath(catalogPath))
            throw new InvalidOperationException("ERP and catalog databases must have different paths.");

        using var target = new SqliteConnection(
            new SqliteConnectionStringBuilder { DataSource = catalogPath, Mode = SqliteOpenMode.ReadWrite, Pooling = false }.ToString());
        target.Open();

        if (IsInitialized(target))
            return;

        // A partially initialized catalogue is retried only if it is empty. Never merge
        // or replace manually supplied Article data without an explicit import operation.
        if (Scalar(target, null, "SELECT COUNT(*) FROM main.Articles") != 0)
            throw new InvalidOperationException(
                "catalog.db contains Articles but has no completed import marker. " +
                "No data has been changed. Back it up and resolve this conflict manually.");

        var hasSource = File.Exists(erpPath);
        if (hasSource)
        {
            using var attach = target.CreateCommand();
            attach.CommandText = "ATTACH DATABASE $source AS legacy";
            attach.Parameters.AddWithValue("$source", erpPath);
            attach.ExecuteNonQuery();

            if (Scalar(target, null,
                    "SELECT COUNT(*) FROM legacy.sqlite_master WHERE type='table' AND name='Articles'") != 1)
                throw new InvalidOperationException("Legacy erp.db has no Articles table; import aborted.");
        }

        using var transaction = target.BeginTransaction();
        var expectedCount = hasSource
            ? Scalar(target, transaction, "SELECT COUNT(*) FROM legacy.Articles")
            : 0;

        if (hasSource && expectedCount > 0)
        {
            var columns = string.Join(", ", ArticleColumns.Select(c => "\"" + c + "\""));
            Execute(target, transaction,
                $"INSERT INTO main.Articles ({columns}) SELECT {columns} FROM legacy.Articles");

            var missingCount = Scalar(target, transaction,
                $"SELECT COUNT(*) FROM (SELECT {columns} FROM legacy.Articles " +
                $"EXCEPT SELECT {columns} FROM main.Articles)");

            if (missingCount != 0 ||
                Scalar(target, transaction, "SELECT COUNT(*) FROM main.Articles") != expectedCount)
                throw new InvalidOperationException("Article verification failed; catalog import rolled back.");
        }

        Execute(target, transaction,
            "CREATE TABLE IF NOT EXISTS main.__CatalogBootstrap " +
            "(Version INTEGER PRIMARY KEY, LegacyArticleCount INTEGER NOT NULL, CompletedUtc TEXT NOT NULL)");
        using (var marker = target.CreateCommand())
        {
            marker.Transaction = transaction;
            marker.CommandText =
                "INSERT INTO main.__CatalogBootstrap (Version, LegacyArticleCount, CompletedUtc) " +
                "VALUES (1, $count, strftime('%Y-%m-%dT%H:%M:%fZ', 'now'))";
            marker.Parameters.AddWithValue("$count", expectedCount);
            marker.ExecuteNonQuery();
        }

        transaction.Commit();
        // Deliberately DO NOT delete Articles in erp.db. They are rollback data.
    }


    /// <summary>
    /// Makes a separate non-overwriting snapshot of the active catalogue immediately
    /// before its old ERP counterpart is deleted.
    /// </summary>
    public static void BackupCatalogBeforeLegacyDrop(string catalogPath)
    {
        var backup = Path.Combine(Path.GetDirectoryName(catalogPath)!, "catalog.pre-legacy-drop.db");
        if (File.Exists(backup)) return;

        var temporary = backup + ".tmp";
        if (File.Exists(temporary)) File.Delete(temporary);
        try
        {
            using (var source = new SqliteConnection(
                new SqliteConnectionStringBuilder { DataSource = catalogPath, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString()))
            using (var destination = new SqliteConnection(
                new SqliteConnectionStringBuilder { DataSource = temporary, Mode = SqliteOpenMode.ReadWriteCreate, Pooling = false }.ToString()))
            {
                source.Open();
                destination.Open();
                source.BackupDatabase(destination);
            }

            if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
                File.SetUnixFileMode(temporary, UnixFileMode.UserRead | UnixFileMode.UserWrite);

            File.Move(temporary, backup);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    /// <summary>
    /// Called by the application's guarded upgrade procedure only. An explicit
    /// SQLite permit is required by the destructive EF migration, so invoking
    /// 'dotnet ef database update' directly cannot silently destroy Articles.
    /// </summary>
    public static void AuthorizeLegacyArticleRemoval(string erpPath, string catalogPath)
    {
        if (Path.GetFullPath(erpPath) == Path.GetFullPath(catalogPath))
            throw new InvalidOperationException("Refusing cleanup: ERP and catalogue have the same path.");

        if (!File.Exists(catalogPath) || !IsInitialized(catalogPath))
            throw new InvalidOperationException("Refusing cleanup: catalogue import is not complete.");

        using var catalog = new SqliteConnection(
            new SqliteConnectionStringBuilder { DataSource = catalogPath, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString());
        catalog.Open();
        CheckIntegrity(catalog, "catalog.db");

        if (Scalar(catalog, null,
                "SELECT COUNT(*) FROM main.sqlite_master WHERE type='table' AND name='Articles'") != 1)
            throw new InvalidOperationException("Refusing cleanup: catalogue has no Articles table.");

        if (Scalar(catalog, null,
                "SELECT COUNT(*) FROM main.sqlite_master WHERE type='table' AND name IN " +
                "('Customers','Contacts','Quotations','Invoices','Contracts','PackingLists','Users')") != 0)
            throw new InvalidOperationException("Refusing cleanup: catalogue contains personal ERP tables.");

        using var erp = new SqliteConnection(
            new SqliteConnectionStringBuilder { DataSource = erpPath, Mode = SqliteOpenMode.ReadWrite, Pooling = false }.ToString());
        erp.Open();
        CheckIntegrity(erp, "erp.db");

        if (Scalar(erp, null,
                "SELECT COUNT(*) FROM main.sqlite_master WHERE type='table' AND name='Articles'") != 1)
            throw new InvalidOperationException("Refusing cleanup: legacy Articles table is unexpectedly absent.");

        var oldCount = Scalar(erp, null, "SELECT COUNT(*) FROM main.Articles");
        var importedCount = Scalar(catalog, null,
            "SELECT LegacyArticleCount FROM main.__CatalogBootstrap WHERE Version=1");
        var currentCount = Scalar(catalog, null, "SELECT COUNT(*) FROM main.Articles");

        if (oldCount > 0)
        {
            if (importedCount != oldCount || currentCount == 0)
                throw new InvalidOperationException(
                    "Refusing cleanup: legacy Article count differs from the original import " +
                    "or the active catalogue is empty. Resolve the mismatch before cleanup.");

            var backupPath = Path.Combine(Path.GetDirectoryName(erpPath)!, "erp.pre-catalog-split.db");
            if (!File.Exists(backupPath))
                throw new InvalidOperationException("Refusing cleanup: the original ERP backup is missing.");

            // Verify that the historical source has not been edited since it was
            // backed up. Older backups may have fewer audit columns, so compare
            // all the columns that existed at backup time.
            using var backup = new SqliteConnection(
                new SqliteConnectionStringBuilder { DataSource = backupPath, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString());
            backup.Open();
            CheckIntegrity(backup, "ERP backup");

            var backupColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using (var info = backup.CreateCommand())
            {
                info.CommandText = "PRAGMA table_info('Articles')";
                using var reader = info.ExecuteReader();
                while (reader.Read()) backupColumns.Add(reader.GetString(1));
            }

            var columns = ArticleColumns.Where(c => backupColumns.Contains(c))
                .Select(c => "\"" + c + "\"").ToArray();
            if (!backupColumns.Contains("Id") || columns.Length < 2)
                throw new InvalidOperationException("Refusing cleanup: the original ERP backup is invalid.");

            using (var attach = erp.CreateCommand())
            {
                attach.CommandText = "ATTACH DATABASE $path AS old_backup";
                attach.Parameters.AddWithValue("$path", backupPath);
                attach.ExecuteNonQuery();
            }

            var fields = string.Join(", ", columns);
            if (Scalar(erp, null, "SELECT COUNT(*) FROM (SELECT " + fields +
                    " FROM main.Articles EXCEPT SELECT " + fields + " FROM old_backup.Articles)") != 0 ||
                Scalar(erp, null, "SELECT COUNT(*) FROM (SELECT " + fields +
                    " FROM old_backup.Articles EXCEPT SELECT " + fields + " FROM main.Articles)") != 0)
                throw new InvalidOperationException(
                    "Refusing cleanup: original ERP Articles have changed since the backup.");
        }

        using var permit = erp.CreateCommand();
        permit.CommandText =
            "CREATE TABLE IF NOT EXISTS __CatalogSplitPermit " +
            "(Version INTEGER PRIMARY KEY, VerifiedUtc TEXT NOT NULL);" +
            "INSERT OR REPLACE INTO __CatalogSplitPermit (Version, VerifiedUtc) " +
            "VALUES (1, strftime('%Y-%m-%dT%H:%M:%fZ','now'));";
        permit.ExecuteNonQuery();
    }

    private static void CheckIntegrity(SqliteConnection connection, string label)
    {
        using var check = connection.CreateCommand();
        check.CommandText = "PRAGMA quick_check";
        if (!string.Equals(Convert.ToString(check.ExecuteScalar(), CultureInfo.InvariantCulture),
                "ok", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Refusing cleanup: integrity check failed for " + label);
    }

    private static bool IsInitialized(string catalogPath)
    {
        using var connection = new SqliteConnection(
            new SqliteConnectionStringBuilder { DataSource = catalogPath, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString());
        connection.Open();
        return IsInitialized(connection);
    }

    private static bool IsInitialized(SqliteConnection connection)
        => Scalar(connection, null,
               "SELECT COUNT(*) FROM main.sqlite_master " +
               "WHERE type='table' AND name='__CatalogBootstrap'") == 1 &&
           Scalar(connection, null,
               "SELECT COUNT(*) FROM main.__CatalogBootstrap WHERE Version=1") == 1;

    private static long Scalar(SqliteConnection connection, SqliteTransaction? transaction, string sql)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        return Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    private static void Execute(SqliteConnection connection, SqliteTransaction transaction, string sql)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }
}
