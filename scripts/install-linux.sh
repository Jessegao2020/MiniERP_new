#!/usr/bin/env bash
# Install or update MiniERP for the current Linux desktop user.
set -euo pipefail

if [[ ${EUID} -eq 0 ]]; then
    echo "Run this script as your normal desktop user, not with sudo." >&2
    exit 1
fi

source_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
binary="MiniERP.Desktop.Avalonia"
data_home="${XDG_DATA_HOME:-$HOME/.local/share}"
app_dir="$HOME/.local/opt/MiniERP"
menu_dir="$data_home/applications"
icon_dir="$data_home/icons/hicolor/256x256/apps"
desktop_id="minierp.desktop"

for file in "$source_dir/$binary" "$source_dir/app.png"; do
    if [[ ! -f "$file" ]]; then
        echo "Missing required file: $file" >&2
        exit 1
    fi
done

mkdir -p -- "$app_dir" "$menu_dir" "$icon_dir"

# Replace the executable atomically, including when the previous version is running.
# Existing database and settings directories are never touched.
temp_binary="$(mktemp "$app_dir/.MiniERP.XXXXXXXX")"
trap 'rm -f -- "$temp_binary"' EXIT
install -m 755 -- "$source_dir/$binary" "$temp_binary"
mv -f -- "$temp_binary" "$app_dir/$binary"
install -m 644 -- "$source_dir/app.png" "$icon_dir/minierp.png"

# Escape paths for the Desktop Entry specification (including spaces and percent).
desktop_exec_path() {
    local path="$1"
    path="${path//\\/\\\\}"
    path="${path//\"/\\\"}"
    path="${path//\$/\\\$}"
    path="${path//\`/\\\`}"
    path="${path//%/%%}"
    path="${path//\\/\\\\}"
    printf '"%s"' "$path"
}
exec_path="$(desktop_exec_path "$app_dir/$binary")"

cat > "$menu_dir/$desktop_id" <<EOF
[Desktop Entry]
Version=1.0
Type=Application
Name=MiniERP
Comment=MiniERP business management
Exec=$exec_path
Icon=minierp
Terminal=false
Categories=Office;
StartupNotify=true
StartupWMClass=MiniERP
EOF
chmod 644 "$menu_dir/$desktop_id"

if command -v update-desktop-database >/dev/null 2>&1; then
    update-desktop-database "$menu_dir" >/dev/null 2>&1 || true
fi
if command -v gtk-update-icon-cache >/dev/null 2>&1; then
    gtk-update-icon-cache -f -t "$data_home/icons/hicolor" >/dev/null 2>&1 || true
fi

desktop_dir="$HOME/Desktop"
if command -v xdg-user-dir >/dev/null 2>&1; then
    desktop_dir="$(xdg-user-dir DESKTOP)"
fi

if [[ -n "$desktop_dir" && "$desktop_dir" != "$HOME" ]]; then
    mkdir -p -- "$desktop_dir"
    install -m 755 -- "$menu_dir/$desktop_id" "$desktop_dir/$desktop_id"
    if command -v gio >/dev/null 2>&1; then
        gio set "$desktop_dir/$desktop_id" metadata::trusted true 2>/dev/null || true
    fi
else
    echo "Desktop directory disabled; created the application menu entry only."
fi

echo "MiniERP installed: $app_dir/$binary"
echo "Find MiniERP in the application menu. Right-click it and choose Add to panel (or Pin to panel)."
echo "To upgrade, close MiniERP and run this script from the new release."
