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

## Linux desktop icon and application menu

The project keeps `Desktop/MiniERP.Desktop.Avalonia/Assets/app.ico` for the
native Windows EXE icon. It also contains `Assets/app.png`, extracted from the
256×256 PNG frame already embedded in that same ICO, for Linux.

The Avalonia window uses `Icon="/Assets/app.png"` on both platforms.
On Linux, `dotnet run` alone does not register an application in the Cinnamon
menu or associate a dock launcher with an icon. To install a user-local
published application, menu entry, and 256×256 icon, run from the repository root:

```bash
bash scripts/install-linux-launcher.sh
```

The installer publishes Release `linux-x64` self-contained, installs to
`~/.local/share/minierp/app`, and writes
`~/.local/share/applications/minierp.desktop` and the matching hicolor icon
in `~/.local/share/icons/hicolor/256x256/apps`. It does not require sudo.
Launch the installed version from the desktop's MiniERP menu entry. Re-run the
script to update the installed app after pulling code changes.

The `.desktop` launcher is separate from the Windows `<ApplicationIcon>`
setting in the project file; `<ApplicationIcon>` alone does not install a
Linux desktop launcher.

## Phase 1 goals

- Prove Avalonia starts on Linux.
- Reuse the existing Domain, Application, EF Core and SQLite layers.
- Stop storing the live database beside the executable.
- Keep the WPF project untouched while screens are migrated incrementally.

Next recommended step: migrate Article list/edit first, then Customer list/edit, then replace the WPF-only FilterableDataGrid.
