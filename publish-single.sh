#!/usr/bin/env bash
# One command on Linux Mint: publish self-contained single-file Linux and Windows binaries.
set -Eeuo pipefail

repo_root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)"
cd "$repo_root"

project="Desktop/MiniERP.Desktop.Avalonia/MiniERP.Desktop.Avalonia.csproj"
output="$repo_root/publish/single-file"

if ! command -v dotnet >/dev/null 2>&1; then
  echo "ERROR: .NET 8 SDK is required. 'dotnet' was not found." >&2
  exit 1
fi

mkdir -p "$output"
staging="$(mktemp -d "$output/.staging.XXXXXXXX")"
trap 'rm -rf -- "$staging"' EXIT

publish_one() {
  local rid="$1"
  local binary="$2"
  local folder="$staging/$rid"
  mkdir -p "$folder"

  echo
  echo "========== Publishing $rid (single-file, self-contained) =========="
  dotnet publish "$project" \
    -c Release \
    -r "$rid" \
    --self-contained true \
    -p:PublishSingleFile=true \
    -p:IncludeNativeLibrariesForSelfExtract=true \
    -p:PublishTrimmed=false \
    -p:PublishReadyToRun=false \
    -p:DebugType=None \
    -p:DebugSymbols=false \
    -o "$folder"

  local source="$folder/$binary"
  if [[ ! -s "$source" ]]; then
    echo "ERROR: Expected single-file executable is missing: $source" >&2
    exit 1
  fi

  # Fail rather than silently shipping an executable that needs sidecar libraries.
  local unexpected=""
  unexpected="$(find "$folder" -maxdepth 1 -type f ! -name "$binary" -print)"
  if [[ -n "$unexpected" ]]; then
    echo "ERROR: Publish produced additional files; not safe to ship only the exe:" >&2
    echo "$unexpected" >&2
    exit 1
  fi
}

publish_one "linux-x64" "MiniERP.Desktop.Avalonia"
publish_one "win-x64" "MiniERP.Desktop.Avalonia.exe"

# Replace release deliverables only after BOTH publishes succeeded.
install -m 755 "$staging/linux-x64/MiniERP.Desktop.Avalonia" "$output/MiniERP-linux-x64"
install -m 644 "$staging/win-x64/MiniERP.Desktop.Avalonia.exe" "$output/MiniERP-win-x64.exe"

echo
echo "SUCCESS — both standalone executables:"
ls -lh "$output/MiniERP-linux-x64" "$output/MiniERP-win-x64.exe"
echo
echo "Linux:   $output/MiniERP-linux-x64"
echo "Windows: $output/MiniERP-win-x64.exe"
echo "Note: Article/customer databases are NOT included in either executable."
