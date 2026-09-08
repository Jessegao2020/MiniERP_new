# MiniERP Avalonia Linux port — Phase 1

This project is an isolated cross-platform desktop shell. It does not modify or replace the existing WPF UI.

## Run on Linux Mint

```bash
dotnet restore Desktop/MiniERP.Desktop.Avalonia/MiniERP.Desktop.Avalonia.csproj
dotnet run --project Desktop/MiniERP.Desktop.Avalonia/MiniERP.Desktop.Avalonia.csproj
```

If NuGet downloads time out, retry with parallel restore disabled:

```bash
dotnet restore Desktop/MiniERP.Desktop.Avalonia/MiniERP.Desktop.Avalonia.csproj --disable-parallel
```

If a previous download was interrupted and the cache is suspect, clear NuGet's HTTP cache and retry:

```bash
dotnet nuget locals http-cache --clear
dotnet restore Desktop/MiniERP.Desktop.Avalonia/MiniERP.Desktop.Avalonia.csproj --disable-parallel
```

The database is created/migrated in the user's local application-data directory. On a typical Linux desktop this resolves to:

```text
~/.local/share/MiniERP/erp.db
```

## Phase 1 goals

- Prove Avalonia starts on Linux.
- Reuse the existing Domain, Application, EF Core and SQLite layers.
- Stop storing the live database beside the executable.
- Keep the WPF project untouched while screens are migrated incrementally.

Next recommended step: migrate Article list/edit first, then Customer list/edit, then replace the WPF-only FilterableDataGrid.
