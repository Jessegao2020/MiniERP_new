using System.Globalization;
using Microsoft.Data.Sqlite;

namespace MiniERP.Infrastructure.Data;

/// <summary>
/// Copies historical Articles into the new catalog without deleting or modifying any
/// business record in erp.db. It is safe to resume after a failed first launch.
/// Run after both DbContexts have applied their EF Core migrations.
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
        if (!File.Exists(erpPath) || (File.Exists(catalogPath) && IsInitialized(catalogPath)))
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
                new SqliteConnectionStringBuilder { DataSource = erpPath, Mode = SqliteOpenMode.ReadOnly }.ToString()))
            using (var destination = new SqliteConnection(
                new SqliteConnectionStringBuilder { DataSource = temporaryPath, Mode = SqliteOpenMode.ReadWriteCreate }.ToString()))
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
            new SqliteConnectionStringBuilder { DataSource = catalogPath, Mode = SqliteOpenMode.ReadWrite }.ToString());
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

    private static bool IsInitialized(string catalogPath)
    {
        using var connection = new SqliteConnection(
            new SqliteConnectionStringBuilder { DataSource = catalogPath, Mode = SqliteOpenMode.ReadOnly }.ToString());
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
