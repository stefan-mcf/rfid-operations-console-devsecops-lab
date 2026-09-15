#!/usr/bin/env bash
set -euo pipefail

project_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$project_root"
export RFID_OPS_HOST_ADDRESS="${RFID_OPS_HOST_ADDRESS:-127.0.0.1}"

require_value() {
  local name="$1"
  if [[ -z "${!name:-}" ]]; then
    echo "Required environment variable is missing: $name" >&2
    exit 1
  fi
}

wait_for_url() {
  local url="$1"
  local attempts="${2:-30}"
  local delay_seconds="${3:-2}"

  for ((attempt = 1; attempt <= attempts; attempt++)); do
    if curl --fail --silent --show-error "$url" >/dev/null; then
      return 0
    fi
    sleep "$delay_seconds"
  done

  echo "Timed out waiting for $url" >&2
  return 1
}

mkdir -p artifacts .pipeline
