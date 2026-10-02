#!/usr/bin/env bash
source "$(dirname "$0")/common.sh"

require_value RFID_OPS_VERSION
require_value RFID_OPS_READER_KEY
require_value RFID_OPS_ADMIN_KEY
case "$RFID_OPS_HOST_ADDRESS" in
  127.0.0.1|localhost|host.docker.internal) ;;
  *) echo "The rollback drill requires a local host." >&2; exit 1 ;;
esac

evidence_directory="artifacts/rollback-drill"
mkdir -p "$evidence_directory"
rm -f "$evidence_directory/drill-manifest.json" "$evidence_directory/controlled-failure.json" \
  "$evidence_directory/rollback/rollback-manifest.json"
previous_image="$(docker inspect rfid-ops-production-app-1 --format '{{.Config.Image}}' 2>/dev/null || true)"
if [[ -z "$previous_image" ]]; then
  jq -n '{status: "skipped", reason: "No previous local release exists"}' \
    > "$evidence_directory/drill-manifest.json"
  echo "A rollback drill requires an existing release; the first release will establish it."
  exit 0
fi

curl --fail --silent --show-error --max-time 5 \
  "http://${RFID_OPS_HOST_ADDRESS}:${RFID_OPS_PRODUCTION_PORT:-28080}/health" \
  | jq -e '.status == "healthy" and .database == "reachable"' >/dev/null

set +e
RFID_OPS_RELEASE_DRILL=true RFID_OPS_RELEASE_EVIDENCE_DIRECTORY="$evidence_directory" \
  scripts/ci/release.sh
result=$?
set -e
test "$result" -ne 0
test -s "$evidence_directory/controlled-failure.json"
jq -e --arg previous "$previous_image" \
  '.status == "restored" and .image == $previous and .smoke == "passed"' \
  "$evidence_directory/rollback/rollback-manifest.json" >/dev/null
test "$(docker inspect rfid-ops-production-app-1 --format '{{.Config.Image}}')" = "$previous_image"
jq -n --arg previous "$previous_image" --argjson releaseExitCode "$result" \
  '{status: "passed", previousImage: $previous, releaseExitCode: $releaseExitCode,
    failure: "stopped candidate failed its health check", automaticRollback: "passed"}' \
  > "$evidence_directory/drill-manifest.json"
echo "Controlled local release failure restored $previous_image and passed smoke checks."
