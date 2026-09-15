#!/usr/bin/env bash
source "$(dirname "$0")/common.sh"

require_value RFID_OPS_VERSION
require_value RFID_OPS_READER_KEY
require_value RFID_OPS_ADMIN_KEY

mkdir -p .pipeline/release artifacts/release
source_image="${RFID_OPS_IMAGE_REPOSITORY:-rfid-ops}:${RFID_OPS_VERSION}"
release_image="${RFID_OPS_IMAGE_REPOSITORY:-rfid-ops}:release-${RFID_OPS_VERSION}"
previous_image="$(docker inspect rfid-ops-production-app-1 --format '{{.Config.Image}}' 2>/dev/null || true)"

if [[ -n "$previous_image" ]]; then
  printf '%s\n' "$previous_image" > .pipeline/release/previous-image
fi

docker tag "$source_image" "$release_image"
export RFID_OPS_IMAGE="$release_image"
export RFID_OPS_PRODUCTION_PORT="${RFID_OPS_PRODUCTION_PORT:-28080}"

rollback_on_failure() {
  if [[ -n "$previous_image" ]]; then
    echo "Release verification failed; restoring $previous_image" >&2
    RFID_OPS_IMAGE="$previous_image" docker compose \
      --project-name rfid-ops-production \
      --file deploy/compose.production.yml \
      up --detach --force-recreate --wait
  fi
}
trap rollback_on_failure ERR

docker compose \
  --project-name rfid-ops-production \
  --file deploy/compose.production.yml \
  up --detach --force-recreate --wait

export RFID_OPS_BASE_URL="http://${RFID_OPS_HOST_ADDRESS}:${RFID_OPS_PRODUCTION_PORT}"
export RFID_OPS_EVIDENCE_DIRECTORY="artifacts/release"
scripts/ci/smoke.sh

docker image inspect "$release_image" > artifacts/release/release-image-inspect.json
docker compose \
  --project-name rfid-ops-production \
  --file deploy/compose.production.yml \
  ps --format json > artifacts/release/compose-state.json

printf '%s\n' "$release_image" > .pipeline/release/current-image
trap - ERR
echo "Released ${release_image} to the production-like local environment."
