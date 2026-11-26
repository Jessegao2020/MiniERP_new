#!/usr/bin/env bash
# Run from Linux (or macOS) with the .NET 8 SDK installed.
set -euo pipefail
root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
project_dir="$root/Desktop/MiniERP.Desktop.Avalonia"
project="$project_dir/MiniERP.Desktop.Avalonia.csproj"
output="$root/publish/single-file"
binary="MiniERP.Desktop.Avalonia"

case "${1:-all}" in
    all) targets=(linux-x64 win-x64) ;;
    linux|linux-x64) targets=(linux-x64) ;;
    windows|win-x64) targets=(win-x64) ;;
    *) echo "Usage: bash publish-single-file.sh [all|linux|windows]" >&2; exit 2 ;;
esac

command -v dotnet >/dev/null 2>&1 || { echo ".NET 8 SDK is required on the build machine." >&2; exit 1; }
work_dir="$(mktemp -d)"
trap 'rm -rf -- "$work_dir"' EXIT

for rid in "${targets[@]}"; do
    build_dir="$work_dir/$rid"
    exe="$binary"
    if [[ "$rid" == win-x64 ]]; then
        exe="$binary.exe"
    fi

    dotnet publish "$project" -c Release -r "$rid" \
        --self-contained true \
        -p:PublishSingleFile=true \
        -p:IncludeNativeLibrariesForSelfExtract=true \
        -p:PublishTrimmed=false \
        -p:DebugType=None \
        -p:DebugSymbols=false \
        -o "$build_dir"

    if [[ ! -f "$build_dir/$exe" ]]; then
        echo "Expected executable missing: $build_dir/$exe" >&2
        exit 1
    fi
    extra="$(find "$build_dir" -mindepth 1 -maxdepth 1 ! -name "$exe" -print)"
    if [[ -n "$extra" ]]; then
        echo "Unexpected additional publish files; refusing to distribute an incomplete single-file build:" >&2
        printf '%s\n' "$extra" >&2
        exit 1
    fi

    dest="$output/$rid"
    mkdir -p -- "$dest"
    if [[ "$rid" == linux-x64 ]]; then
        install -m 755 -- "$build_dir/$exe" "$dest/$exe"
        install -m 644 -- "$project_dir/Assets/app.png" "$dest/app.png"
        install -m 755 -- "$root/scripts/install-linux.sh" "$dest/install.sh"
    else
        install -m 644 -- "$build_dir/$exe" "$dest/$exe"
    fi
    echo "Published: $dest"
done
