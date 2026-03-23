#!/usr/bin/env bash
set -euo pipefail

RID="${1:-linux-x64}"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PACKAGE_SCRIPT="$SCRIPT_DIR/package.ps1"

to_windows_path() {
  local path="$1"

  if [[ "$path" =~ ^/mnt/([a-zA-Z])/(.*)$ ]]; then
    local drive="${BASH_REMATCH[1]^^}"
    local remainder="${BASH_REMATCH[2]//\//\\}"
    printf '%s:\\%s' "$drive" "$remainder"
    return 0
  fi

  printf '%s' "$path"
}

if command -v pwsh >/dev/null 2>&1; then
  PS_CMD=(pwsh -NoProfile -File)
elif command -v powershell.exe >/dev/null 2>&1; then
  PS_CMD=(powershell.exe -NoProfile -ExecutionPolicy Bypass -File)
  PACKAGE_SCRIPT="$(to_windows_path "$PACKAGE_SCRIPT")"
elif command -v powershell >/dev/null 2>&1; then
  PS_CMD=(powershell -NoProfile -ExecutionPolicy Bypass -File)
  PACKAGE_SCRIPT="$(to_windows_path "$PACKAGE_SCRIPT")"
else
  echo "PowerShell is required to package OpenGarrison."
  exit 1
fi

"${PS_CMD[@]}" "$PACKAGE_SCRIPT" -Platforms "$RID" -SkipTests
