#!/usr/bin/env bash
source "$(dirname "$0")/common.sh"

require_value RFID_OPS_VERSION
require_value RFID_OPS_READER_KEY
require_value RFID_OPS_ADMIN_KEY

export RFID_OPS_IMAGE="${RFID_OPS_IMAGE_REPOSITORY:-rfid-ops}:${RFID_OPS_VERSION}"
export RFID_OPS_STAGING_PORT="${RFID_OPS_STAGING_PORT:-18080}"

docker compose \
  --project-name rfid-ops-staging \
  --file deploy/compose.staging.yml \
  up --detach --force-recreate --wait

export RFID_OPS_BASE_URL="http://${RFID_OPS_HOST_ADDRESS}:${RFID_OPS_STAGING_PORT}"
export RFID_OPS_EVIDENCE_DIRECTORY="artifacts/deploy"
scripts/ci/smoke.sh

docker compose \
  --project-name rfid-ops-staging \
  --file deploy/compose.staging.yml \
  ps --format json > artifacts/deploy/compose-state.json
