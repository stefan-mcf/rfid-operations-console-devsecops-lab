#!/usr/bin/env bash
source "$(dirname "$0")/common.sh"

require_value RFID_OPS_READER_KEY
require_value RFID_OPS_ADMIN_KEY
export RFID_OPS_EVIDENCE_DIRECTORY="${RFID_OPS_ROLLBACK_EVIDENCE_DIRECTORY:-artifacts/rollback}"
mkdir -p "$RFID_OPS_EVIDENCE_DIRECTORY"
rm -f "$RFID_OPS_EVIDENCE_DIRECTORY/rollback-manifest.json"

previous_image_file=".pipeline/release/previous-image"
if [[ ! -s "$previous_image_file" || ! -s .pipeline/release/previous-image-id ]]; then
  echo "No previous release image has been recorded." >&2
  exit 1
fi

export RFID_OPS_IMAGE="$(<"$previous_image_file")"
expected_image_id="$(<.pipeline/release/previous-image-id)"
image_id="$(docker image inspect "$RFID_OPS_IMAGE" --format '{{.Id}}')"
test -n "$image_id"
test "$image_id" = "$expected_image_id"
commit="$(docker image inspect "$RFID_OPS_IMAGE" \
  --format '{{ index .Config.Labels "org.opencontainers.image.revision" }}')"
test -n "$commit"
export RFID_OPS_VERSION="$(docker image inspect "$RFID_OPS_IMAGE" \
  --format '{{ index .Config.Labels "org.opencontainers.image.version" }}')"
require_value RFID_OPS_VERSION
export RFID_OPS_PRODUCTION_PORT="${RFID_OPS_PRODUCTION_PORT:-28080}"

docker compose \
  --project-name rfid-ops-production \
  --file deploy/compose.production.yml \
  up --detach --force-recreate --wait

export RFID_OPS_BASE_URL="http://${RFID_OPS_HOST_ADDRESS}:${RFID_OPS_PRODUCTION_PORT}"
scripts/ci/smoke.sh

test "$(docker inspect rfid-ops-production-app-1 --format '{{.Image}}')" = "$image_id"

docker image inspect "$RFID_OPS_IMAGE" > "$RFID_OPS_EVIDENCE_DIRECTORY/image-inspect.json"
docker compose --project-name rfid-ops-production --file deploy/compose.production.yml \
  ps --format json > "$RFID_OPS_EVIDENCE_DIRECTORY/compose-state.json"
jq -n --arg image "$RFID_OPS_IMAGE" \
  --arg fromImage "${RFID_OPS_ROLLBACK_FROM_IMAGE:-unknown}" \
  --arg imageId "$image_id" \
  --arg commit "$commit" \
  --arg version "$RFID_OPS_VERSION" --arg target "$RFID_OPS_BASE_URL" \
  '{status: "restored", fromImage: $fromImage, image: $image, imageId: $imageId,
    commit: $commit, version: $version, target: $target, smoke: "passed"}' \
  > "$RFID_OPS_EVIDENCE_DIRECTORY/rollback-manifest.json"
printf '%s\n' "$RFID_OPS_IMAGE" > .pipeline/release/current-image
echo "Restored ${RFID_OPS_IMAGE}; health and protected-route smoke checks passed."
