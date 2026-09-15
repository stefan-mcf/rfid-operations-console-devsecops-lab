#!/usr/bin/env bash
source "$(dirname "$0")/common.sh"

require_value RFID_OPS_READER_KEY
require_value RFID_OPS_ADMIN_KEY
require_value RFID_OPS_BASE_URL

evidence_directory="${RFID_OPS_EVIDENCE_DIRECTORY:-artifacts/smoke}"
mkdir -p "$evidence_directory"

wait_for_url "${RFID_OPS_BASE_URL}/health" 30 2
curl --fail --silent --show-error "${RFID_OPS_BASE_URL}/health" \
  | tee "${evidence_directory}/health.json" \
  | jq -e '.status == "healthy" and .database == "reachable"' >/dev/null

curl --fail --silent --show-error "${RFID_OPS_BASE_URL}/" \
  | tee "${evidence_directory}/dashboard.html" \
  | grep -q "RFID OPERATIONS / LOCAL SIMULATION"
grep -q 'id="tag-form"' "${evidence_directory}/dashboard.html"
grep -q 'id="event-form"' "${evidence_directory}/dashboard.html"

tag_id="DEMO-${BUILD_NUMBER:-LOCAL}-$(date +%s)"
curl --fail --silent --show-error \
  --request PUT \
  --header "Content-Type: application/json" \
  --header "X-Admin-Key: ${RFID_OPS_ADMIN_KEY}" \
  --data '{"displayName":"Pipeline smoke tag","state":"Active"}' \
  "${RFID_OPS_BASE_URL}/api/v1/tags/${tag_id}" \
  | tee "${evidence_directory}/tag-registration.json" \
  | jq -e '.state == "Active"' >/dev/null

curl --fail --silent --show-error \
  --request POST \
  --header "Content-Type: application/json" \
  --header "X-Reader-Key: ${RFID_OPS_READER_KEY}" \
  --data "{\"tagId\":\"${tag_id}\",\"readerId\":\"PIPELINE-READER-01\"}" \
  "${RFID_OPS_BASE_URL}/api/v1/events" \
  | tee "${evidence_directory}/access-decision.json" \
  | jq -e '.outcome == "Granted" and .reason == "ActiveRegistration"' >/dev/null

curl --fail --silent --show-error "${RFID_OPS_BASE_URL}/metrics" \
  | tee "${evidence_directory}/metrics.txt" \
  | grep -q "rfid_ops_access_decisions_total"

echo "Smoke test passed for ${RFID_OPS_BASE_URL} using synthetic event ${tag_id}."
