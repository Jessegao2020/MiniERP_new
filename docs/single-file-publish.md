# One-command single-file publishing for Linux Mint

Run from the repository (or any current working directory):

    bash ./publish-single.sh

Alternatively, after a normal Git checkout:

    ./publish-single.sh

Requires .NET 8 SDK, initial NuGet/runtime pack downloads, and Linux Mint on x86-64. It publishes **linux-x64** and **win-x64** from the Avalonia project, Release configuration, self-contained .NET runtime, and bundled native libraries. Trimming is disabled because the ERP uses EF Core migrations and reflection; AOT is not used.

The two distributable files are:

    publish/single-file/MiniERP-linux-x64
    publish/single-file/MiniERP-win-x64.exe

Deliverables are replaced only after both target publishes succeed. The script checks for unexpected extra output files instead of quietly omitting dependencies. Intermediate outputs are temporary.

## Running

Linux:

    ./publish/single-file/MiniERP-linux-x64

Windows: copy **MiniERP-win-x64.exe** to an x64 Windows computer and double-click it. The existing \`Assets/app.ico\` icon is embedded in the Windows executable.

Self-contained publishing means .NET does not need to be installed on recipients' computers, but the app may extract bundled native libraries to an OS-specific user-writable cache on first launch. This is not an installer or a portable data directory: application databases remain separate.

- Linux personal data: \`~/.local/share/MiniERP/erp.db\` and \`catalog.db\`.
- Windows personal data: \`%LOCALAPPDATA%\MiniERP\erp.db\` and \`catalog.db\`.
- A colleague receives only their executable and any explicitly shared catalog database; never send \`erp.db\`, customer information or private backups.

The app supports the headless \`--verify-bundle\` switch. GitHub Actions invokes it on Linux and on the **Windows executable cross-published on Linux** via a real Windows runner. These checks complement, but cannot fully replace, testing the desktop UI on physical machines.

If Windows refuses to launch, inspect \`%LOCALAPPDATA%\MiniERP\startup-error.log\` and Event Viewer. Never delete databases as a launch workaround.
