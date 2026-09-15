#!/usr/bin/env bash
source "$(dirname "$0")/common.sh"

require_value RFID_OPS_VERSION
require_value RFID_OPS_READER_KEY
require_value RFID_OPS_ADMIN_KEY

previous_image_file=".pipeline/release/previous-image"
if [[ ! -s "$previous_image_file" ]]; then
  echo "No previous release image has been recorded." >&2
  exit 1
fi

export RFID_OPS_IMAGE="$(<"$previous_image_file")"
export RFID_OPS_PRODUCTION_PORT="${RFID_OPS_PRODUCTION_PORT:-28080}"

docker compose \
  --project-name rfid-ops-production \
  --file deploy/compose.production.yml \
  up --detach --force-recreate --wait

export RFID_OPS_BASE_URL="http://${RFID_OPS_HOST_ADDRESS}:${RFID_OPS_PRODUCTION_PORT}"
export RFID_OPS_EVIDENCE_DIRECTORY="artifacts/rollback"
scripts/ci/smoke.sh
