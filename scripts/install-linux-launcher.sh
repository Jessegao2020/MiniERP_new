#!/usr/bin/env bash
# Publish MiniERP into the current user's home and install a Linux app launcher.
# Run from any directory: bash scripts/install-linux-launcher.sh
set -euo pipefail

if [[ "$(uname -s)" != "Linux" ]]; then
  echo "This installer is for Linux only." >&2
  exit 1
fi
if ! command -v dotnet >/dev/null 2>&1; then
  echo "The .NET 8 SDK is required to publish MiniERP." >&2
  exit 1
fi

project_root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
project="$project_root/Desktop/MiniERP.Desktop.Avalonia/MiniERP.Desktop.Avalonia.csproj"
png="$project_root/Desktop/MiniERP.Desktop.Avalonia/Assets/app.png"
data_home="${XDG_DATA_HOME:-$HOME/.local/share}"
app_dir="$data_home/minierp/app"
icons_dir="$data_home/icons/hicolor/256x256/apps"
desktop_dir="$data_home/applications"

if [[ ! -f "$png" ]]; then
  echo "Missing MiniERP icon: $png" >&2
  exit 1
fi

echo "Publishing MiniERP for Linux x64 ..."
mkdir -p "$app_dir" "$icons_dir" "$desktop_dir"
dotnet publish "$project" -c Release -r linux-x64 --self-contained true -o "$app_dir"

cp "$png" "$icons_dir/minierp.png"
chmod +x "$app_dir/MiniERP.Desktop.Avalonia"

# Linux desktop launchers use a separate desktop-entry/icon registration.
# XAML Window.Icon by itself does not guarantee a menu or dock icon.
cat > "$desktop_dir/minierp.desktop" <<EOF
[Desktop Entry]
Type=Application
Version=1.0
Name=MiniERP
Comment=MiniERP desktop business application
Exec="$app_dir/MiniERP.Desktop.Avalonia"
Path=$app_dir
Icon=minierp
Terminal=false
StartupNotify=true
StartupWMClass=MiniERP.Desktop.Avalonia
Categories=Office;
EOF

if command -v update-desktop-database >/dev/null 2>&1; then
  update-desktop-database "$desktop_dir" >/dev/null 2>&1 || true
fi
if command -v gtk-update-icon-cache >/dev/null 2>&1; then
  gtk-update-icon-cache -f -t "$data_home/icons/hicolor" >/dev/null 2>&1 || true
fi

echo
echo "MiniERP installed for this user."
echo "Launcher: $desktop_dir/minierp.desktop"
echo "Icon:     $icons_dir/minierp.png"
echo "Search for 'MiniERP' in your desktop application menu."
echo "No root/sudo privileges are needed."
