#!/usr/bin/env bash
source "$(dirname "$0")/common.sh"

require_value RFID_OPS_VERSION
require_value RFID_OPS_READER_KEY
require_value RFID_OPS_ADMIN_KEY
if [[ "${RFID_OPS_RELEASE_DRILL:-false}" == true ]]; then
  case "$RFID_OPS_HOST_ADDRESS" in
    127.0.0.1|localhost|host.docker.internal) ;;
    *) echo "The rollback drill requires a local host." >&2; exit 1 ;;
  esac
fi

evidence_directory="${RFID_OPS_RELEASE_EVIDENCE_DIRECTORY:-artifacts/release}"
mkdir -p .pipeline/release "$evidence_directory"
rm -f "$evidence_directory/release-manifest.json" "$evidence_directory/git-tag.json" \
  "$evidence_directory/controlled-failure.json"
source_image="${RFID_OPS_IMAGE_REPOSITORY:-rfid-ops}:${RFID_OPS_VERSION}"
release_image="${RFID_OPS_IMAGE_REPOSITORY:-rfid-ops}:release-${RFID_OPS_VERSION}"
previous_image="$(docker inspect rfid-ops-production-app-1 --format '{{.Config.Image}}' 2>/dev/null || true)"

if [[ -n "$previous_image" ]]; then
  previous_image_id="$(docker inspect rfid-ops-production-app-1 --format '{{.Image}}')"
  test -n "$previous_image_id"
  test "$(docker image inspect "$previous_image_id" --format '{{.Id}}')" = "$previous_image_id"
  previous_version="$(docker image inspect "$previous_image_id" \
    --format '{{ index .Config.Labels "org.opencontainers.image.version" }}')"
  previous_commit="$(docker image inspect "$previous_image_id" \
    --format '{{ index .Config.Labels "org.opencontainers.image.revision" }}')"
  test -n "$previous_version"
  test -n "$previous_commit"
  printf '%s\n' "$previous_image" > .pipeline/release/previous-image
  printf '%s\n' "$previous_image_id" > .pipeline/release/previous-image-id
else
  rm -f .pipeline/release/previous-image .pipeline/release/previous-image-id
fi
if [[ "${RFID_OPS_RELEASE_DRILL:-false}" == true ]]; then
  test -n "$previous_image"
fi

docker tag "$source_image" "$release_image"
export RFID_OPS_IMAGE="$release_image"
export RFID_OPS_PRODUCTION_PORT="${RFID_OPS_PRODUCTION_PORT:-28080}"

rollback_on_failure() {
  local result=$?
  trap - ERR
  if [[ -n "$previous_image" ]]; then
    echo "Release verification failed; restoring $previous_image" >&2
    if RFID_OPS_ROLLBACK_FROM_IMAGE="$release_image" \
        RFID_OPS_ROLLBACK_EVIDENCE_DIRECTORY="$evidence_directory/rollback" \
        scripts/ci/rollback.sh; then
      echo "The previous release passed its rollback smoke test."
    else
      echo "Rollback verification failed; inspect $evidence_directory/rollback." >&2
    fi
  fi
  exit "$result"
}
trap rollback_on_failure ERR

docker compose \
  --project-name rfid-ops-production \
  --file deploy/compose.production.yml \
  up --detach --force-recreate --wait

export RFID_OPS_BASE_URL="http://${RFID_OPS_HOST_ADDRESS}:${RFID_OPS_PRODUCTION_PORT}"
export RFID_OPS_EVIDENCE_DIRECTORY="$evidence_directory"

if [[ "${RFID_OPS_RELEASE_DRILL:-false}" == true ]]; then
  docker compose --project-name rfid-ops-production \
    --file deploy/compose.production.yml stop app
  if curl --fail --silent --show-error --max-time 5 "$RFID_OPS_BASE_URL/health" \
      > "$evidence_directory/health-after-stop.json" \
      2> "$evidence_directory/health-after-stop.stderr"; then
    echo "The stopped candidate still answered its health check." >&2
  else
    jq -n --arg target "$RFID_OPS_BASE_URL/health" \
      --arg candidate "$release_image" --arg previous "$previous_image" \
      '{observed: "health-check-failed", target: $target, candidate: $candidate, previous: $previous}' \
      > "$evidence_directory/controlled-failure.json"
  fi
  # A failing command enters the same ERR handler as a failed deployment or smoke test.
  false
fi

scripts/ci/smoke.sh

image_id="$(docker image inspect "$release_image" --format '{{.Id}}')"
source_image_id="$(docker image inspect "$source_image" --format '{{.Id}}')"
commit="$(git rev-parse HEAD)"
test -n "$image_id"
test "$image_id" = "$source_image_id"
test "$(docker inspect rfid-ops-production-app-1 --format '{{.Image}}')" = "$image_id"

docker image inspect "$release_image" > "$evidence_directory/release-image-inspect.json"
docker compose \
  --project-name rfid-ops-production \
  --file deploy/compose.production.yml \
  ps --format json > "$evidence_directory/compose-state.json"

RFID_OPS_RELEASE_EVIDENCE_DIRECTORY="$evidence_directory" scripts/ci/release-tag.sh

jq -n --arg image "$release_image" --arg sourceImage "$source_image" \
  --arg imageId "$image_id" --arg sourceImageId "$source_image_id" \
  --arg previousImage "$previous_image" --arg previousImageId "${previous_image_id:-}" --arg commit "$commit" \
  --arg tag "v${RFID_OPS_VERSION}" --arg target "$RFID_OPS_BASE_URL" \
  '{status: "released", image: $image, sourceImage: $sourceImage, imageId: $imageId,
    sourceImageId: $sourceImageId, previousImage: $previousImage, previousImageId: $previousImageId,
    commit: $commit, tag: $tag, target: $target}' \
  > "$evidence_directory/release-manifest.json"

printf '%s\n' "$release_image" > .pipeline/release/current-image
trap - ERR
echo "Released ${release_image} to the production-like local environment."
