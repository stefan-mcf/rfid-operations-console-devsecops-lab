#!/usr/bin/env bash
source "$(dirname "$0")/common.sh"

require_value RFID_OPS_VERSION
require_value RFID_OPS_READER_KEY
require_value RFID_OPS_ADMIN_KEY
require_value GRAFANA_ADMIN_PASSWORD

mkdir -p artifacts/monitoring
export RFID_OPS_PROMETHEUS_IMAGE="rfid-ops-prometheus:${RFID_OPS_VERSION}"
export RFID_OPS_ALERTMANAGER_IMAGE="rfid-ops-alertmanager:${RFID_OPS_VERSION}"
export RFID_OPS_GRAFANA_IMAGE="rfid-ops-grafana:${RFID_OPS_VERSION}"
export GRAFANA_ADMIN_USER="${GRAFANA_ADMIN_USER:-admin}"
export RFID_OPS_IMAGE="${RFID_OPS_IMAGE_REPOSITORY:-rfid-ops}:release-${RFID_OPS_VERSION}"
export RFID_OPS_PRODUCTION_PORT="${RFID_OPS_PRODUCTION_PORT:-28080}"

docker build --file monitoring/Dockerfile --target prometheus \
  --tag "$RFID_OPS_PROMETHEUS_IMAGE" .
docker build --file monitoring/Dockerfile --target alertmanager \
  --tag "$RFID_OPS_ALERTMANAGER_IMAGE" .
docker build --file monitoring/Dockerfile --target grafana \
  --tag "$RFID_OPS_GRAFANA_IMAGE" .

docker compose \
  --project-name rfid-ops-monitoring \
  --file monitoring/compose.monitoring.yml \
  up --detach --force-recreate --wait

monitoring_base="http://${RFID_OPS_HOST_ADDRESS}"

wait_for_url "${monitoring_base}:29090/-/ready" 30 2
wait_for_url "${monitoring_base}:29093/-/ready" 30 2
wait_for_url "${monitoring_base}:23000/api/health" 30 2

for ((attempt = 1; attempt <= 30; attempt++)); do
  curl --fail --silent --show-error "${monitoring_base}:29090/api/v1/targets" \
    > artifacts/monitoring/targets-before-incident.json
  if jq -e '.data.activeTargets[] | select(.labels.job == "rfid-operations-console" and .health == "up")' \
      artifacts/monitoring/targets-before-incident.json >/dev/null; then
    break
  fi
  if [[ "$attempt" -eq 30 ]]; then
    echo "Prometheus did not observe the released application as healthy." >&2
    exit 1
  fi
  sleep 2
done

curl --fail --silent --show-error "${monitoring_base}:29090/api/v1/rules" \
  > artifacts/monitoring/rules-before-incident.json
curl --fail --silent --show-error \
  --user "${GRAFANA_ADMIN_USER}:${GRAFANA_ADMIN_PASSWORD}" \
  "${monitoring_base}:23000/api/dashboards/uid/rfid-operations-console" \
  > artifacts/monitoring/grafana-dashboard.json

restart_production() {
  docker compose \
    --project-name rfid-ops-production \
    --file deploy/compose.production.yml \
    start app >/dev/null 2>&1 || true
}
trap restart_production EXIT

docker compose \
  --project-name rfid-ops-production \
  --file deploy/compose.production.yml \
  stop app

alert_observed=false
for ((attempt = 1; attempt <= 24; attempt++)); do
  curl --fail --silent --show-error "${monitoring_base}:29093/api/v2/alerts" \
    > artifacts/monitoring/alerts-during-incident.json
  if jq -e 'any(.[]; .labels.alertname == "RfidOpsInstanceDown" and .status.state == "active")' \
      artifacts/monitoring/alerts-during-incident.json >/dev/null; then
    alert_observed=true
    break
  fi
  sleep 3
done

if [[ "$alert_observed" != true ]]; then
  echo "The controlled outage did not trigger RfidOpsInstanceDown." >&2
  exit 1
fi

docker logs rfid-ops-alert-receiver > artifacts/monitoring/local-alert-receiver.log 2>&1
restart_production
wait_for_url "${monitoring_base}:28080/health" 30 2
sleep 10

curl --fail --silent --show-error "${monitoring_base}:29090/api/v1/targets" \
  > artifacts/monitoring/targets-after-recovery.json
curl --fail --silent --show-error "${monitoring_base}:29093/api/v2/alerts" \
  > artifacts/monitoring/alerts-after-recovery.json

trap - EXIT
echo "Monitoring verified and a controlled local outage triggered RfidOpsInstanceDown."
