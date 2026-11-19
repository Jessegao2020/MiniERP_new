# Safe Article catalogue split — MiniERP

Source branch: feature/selectline-grid  
New branch: feature/split-article-catalog

## Files used by Avalonia on Linux Mint

- ~/.local/share/MiniERP/erp.db — existing customers, contacts, quotations, contracts, invoices, packing lists, users and their detail records.
- ~/.local/share/MiniERP/catalog.db — the Article catalogue, with its own EF migration history.
- ~/.local/share/MiniERP/erp.pre-catalog-split.db — one-time consistent SQLite backup of the previous ERP database.

No existing tables or rows are removed from erp.db. Its Articles table is intentionally retained as rollback data. After the split, Avalonia Article operations use **catalog.db exclusively**, so new Article edits no longer update the legacy Articles table.

Sales document lines already store name/description/price snapshots and optional SourceArticleId; the split does not rewrite them.

## Existing Mint installation

1. **Quit every MiniERP process**, and close database editors.
2. Make an independent offline backup of the entire ~/.local/share/MiniERP directory. The automatic safety copy is useful but not a substitute for your own backup.
3. Run:

    git fetch origin
    git switch feature/split-article-catalog
    dotnet run --project Desktop/MiniERP.Desktop.Avalonia/MiniERP.Desktop.Avalonia.csproj

4. At first launch the program backs up erp.db **before applying migrations**, upgrades ERP schema if necessary, creates catalog.db, copies all 16 Article columns including IDs and timestamps, and verifies copied data inside a SQLite transaction. It records completion only after successful verification.

If copying fails, the app does not proceed to its UI and legacy data is unchanged. Retry after resolving the error. An empty partially initialized catalogue can be retried automatically; a non-empty catalogue with no import marker is NEVER overwritten automatically.

## Verify after first launch

Exit MiniERP, then run these commands (install sqlite3 if necessary):

    sqlite3 ~/.local/share/MiniERP/erp.db 'PRAGMA integrity_check;'
    sqlite3 ~/.local/share/MiniERP/catalog.db 'PRAGMA integrity_check;'
    sqlite3 ~/.local/share/MiniERP/erp.db 'SELECT COUNT(*) FROM Articles;'
    sqlite3 ~/.local/share/MiniERP/catalog.db 'SELECT COUNT(*) FROM Articles;'
    sqlite3 ~/.local/share/MiniERP/catalog.db 'SELECT Version, LegacyArticleCount FROM __CatalogBootstrap;'
    sqlite3 ~/.local/share/MiniERP/catalog.db "SELECT name FROM sqlite_master WHERE type='table' ORDER BY name;"

Counts should match immediately after migration, before any subsequent edits. catalog.db should contain Articles and internal EF/bootstrap metadata, but **no Customers, Contacts, Quotations, Invoices, PackingLists, Contracts or Users**.

Open old quotations, invoices, contracts and packing lists, confirm totals/items, and test PDFs. Edit a test Article, exit, restart, and confirm it persists.

## Sharing the Article catalogue

**Never share erp.db, erp.pre-catalog-split.db or the whole MiniERP data directory.** Only consider copying catalog.db. Even Article may contain confidential internal prices, MinimumPrice, Note, CreatedBy, and other metadata. Review or sanitize those fields before sending it to a colleague.

Safest: close the app and copy catalog.db. If it is in use, make a consistent SQLite snapshot:

    sqlite3 ~/.local/share/MiniERP/catalog.db '.backup /tmp/minierp-share-catalog.db'

A colleague starts with an empty personal erp.db and the supplied catalog.db. Their own customer/business records are completely separate.

## Rollback and limitations

- Close the application; switch back to feature/selectline-grid to use the legacy Articles table still in erp.db.
- Any Article changes made in catalog.db **after** the first split are not automatically synced to the legacy table. Keep catalog.db to reconcile those changes if rolling back.
- Do not restore the pre-split database backup over erp.db unless you deliberately want to lose business records entered since the backup.
- Do not delete either database to troubleshoot; preserve both files and inspect the error.
- WPF still uses one ERP database for compatibility (the one-argument AddInfrastructure call), while Avalonia explicitly supplies both database paths.
- EF CLI commands must specify --context ApplicationDbContext or --context CatalogDbContext.
- Run the isolated migration smoke test with:

    dotnet run --project tests/MiniERP.CatalogSmoke/MiniERP.CatalogSmoke.csproj

## Public repository privacy warning

The repository previously included an erp.db file on a **public** GitHub branch. The new branch untracks it and ignores future DB files, but old Git history may still contain that database. If it contains real customer information, investigate exposure and plan history cleanup separately. Git history removal does not guarantee removal of clones or caches.


## Phase 2 — remove the redundant ERP Articles table (2026-11-19)

This branch now includes the EF migration `20261119093000_RemoveLegacyArticles`.
**Do not run DROP TABLE manually and do not run `dotnet ef database update --context ApplicationDbContext` directly.** That direct migration is intentionally guarded and fails if it has not been authorized by the application.

Quit MiniERP, ensure you have preserved the original database backup and your own copy of the active catalog, fetch this branch, then rebuild and start the desktop app. `DatabaseStartupMigrator.Upgrade` backs up the original ERP if necessary, ensures all pre-split ERP migrations are applied, creates/verifies the catalogue, saves `catalog.pre-legacy-drop.db`, verifies that the original ERP Articles are covered by the original backup, and then authorizes the one-time EF migration to drop only `erp.db`'s legacy Articles table. The guard rejects a direct EF CLI call that skips those checks.

After the new startup, with the app closed:

    sqlite3 ~/.local/share/MiniERP/erp.db "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='Articles';"
    sqlite3 ~/.local/share/MiniERP/catalog.db 'SELECT COUNT(*) FROM Articles;'
    sqlite3 ~/.local/share/MiniERP/erp.db 'PRAGMA integrity_check;'
    sqlite3 ~/.local/share/MiniERP/catalog.db 'PRAGMA integrity_check;'
    sqlite3 ~/.local/share/MiniERP/erp.db "SELECT MigrationId FROM __EFMigrationsHistory WHERE MigrationId='20261119093000_RemoveLegacyArticles';"

Expected: 0, the current Article count, ok, ok, and the new cleanup migration ID. Existing customer and sales document records must remain unchanged. After this migration, old builds without the catalog split are not backward compatible with erp.db; use the new application or restore an isolated backup for legacy testing. The current WPF bootstrap is also updated to use two files, although the CI check covers the Linux Avalonia build.

For new colleagues with an existing catalog.db and no personal erp.db, the application creates the business database and immediately removes its temporary legacy Article schema. No second Article table remains.