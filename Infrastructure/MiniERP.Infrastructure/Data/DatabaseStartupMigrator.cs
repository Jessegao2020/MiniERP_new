using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace MiniERP.Infrastructure.Data;

/// <summary>
/// Orders the old ERP migrations, the verified Article copy, and finally
/// the destructive cleanup. Application callers must use this instead of
/// applying ApplicationDbContext.Migrations directly.
/// </summary>
public static class DatabaseStartupMigrator
{
    public const string LastLegacyMigration = "20261111030000_UniqueQuotationNumber";
    public const string CleanupMigration = "20261119093000_RemoveLegacyArticles";

    /// <param name="recordStage">
    /// Optional, best-effort profiling callback. Does not change the migration
    /// order or the rules that protect the ERP and catalogue databases.
    /// </param>
    public static void Upgrade(
        ApplicationDbContext erp,
        CatalogDbContext catalog,
        string erpPath,
        string catalogPath,
        Action<string, TimeSpan>? recordStage = null)
    {
        TimeStep(recordStage, "Check pre-split ERP backup", () =>
            CatalogDatabaseBootstrapper.BackupLegacyDatabaseBeforeFirstSplit(erpPath, catalogPath));

        var alreadyApplied = TimeStep(recordStage, "Read ERP migration history", () =>
            erp.Database.GetAppliedMigrations().ToHashSet(StringComparer.Ordinal));
        if (!alreadyApplied.Contains(CleanupMigration) &&
            !alreadyApplied.Contains(LastLegacyMigration))
        {
            // A first-time install or an older ERP database must receive the
            // pre-split schema BEFORE we attempt to copy its Article rows.
            TimeStep(recordStage, "Apply legacy ERP migrations", () =>
                erp.Database.GetService<IMigrator>().Migrate(LastLegacyMigration));
        }

        TimeStep(recordStage, "Apply catalog migrations", () => catalog.Database.Migrate());
        TimeStep(recordStage, "Check/initialize article catalog", () =>
            CatalogDatabaseBootstrapper.Initialize(erpPath, catalogPath));

        if (!alreadyApplied.Contains(CleanupMigration))
        {
            TimeStep(recordStage, "Back up catalog before ERP cleanup", () =>
                CatalogDatabaseBootstrapper.BackupCatalogBeforeLegacyDrop(catalogPath));
            TimeStep(recordStage, "Authorize legacy article cleanup", () =>
                CatalogDatabaseBootstrapper.AuthorizeLegacyArticleRemoval(erpPath, catalogPath));
        }

        TimeStep(recordStage, "Apply remaining ERP migrations", () => erp.Database.Migrate());
    }

    private static void TimeStep(Action<string, TimeSpan>? record, string name, Action action)
    {
        if (record is null)
        {
            action();
            return;
        }

        var started = Stopwatch.GetTimestamp();
        try
        {
            action();
        }
        finally
        {
            record(name, Stopwatch.GetElapsedTime(started));
        }
    }

    private static T TimeStep<T>(Action<string, TimeSpan>? record, string name, Func<T> action)
    {
        if (record is null)
            return action();

        var started = Stopwatch.GetTimestamp();
        try
        {
            return action();
        }
        finally
        {
            record(name, Stopwatch.GetElapsedTime(started));
        }
    }
}
