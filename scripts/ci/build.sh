#!/usr/bin/env bash
source "$(dirname "$0")/common.sh"

require_value RFID_OPS_VERSION
image_repository="${RFID_OPS_IMAGE_REPOSITORY:-rfid-ops}"
commit_sha="${GIT_COMMIT:-$(git rev-parse HEAD 2>/dev/null || printf 'uncommitted')}"
image="${image_repository}:${RFID_OPS_VERSION}"

docker build \
  --build-arg "APP_VERSION=${RFID_OPS_VERSION}" \
  --build-arg "VCS_REF=${commit_sha}" \
  --tag "$image" \
  .

docker image inspect "$image" > artifacts/build-image-inspect.json
image_id="$(docker image inspect "$image" --format '{{.Id}}')"

jq -n \
  --arg image "$image" \
  --arg imageId "$image_id" \
  --arg version "$RFID_OPS_VERSION" \
  --arg commit "$commit_sha" \
  '{image: $image, imageId: $imageId, version: $version, commit: $commit}' \
  > artifacts/build-manifest.json

echo "Built immutable local image $image ($image_id)"
