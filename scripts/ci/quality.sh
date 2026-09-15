#!/usr/bin/env bash
source "$(dirname "$0")/common.sh"

require_value RFID_OPS_VERSION
require_value SONAR_HOST_URL
require_value SONAR_TOKEN

rm -rf artifacts/quality .sonarqube
mkdir -p artifacts/quality

sonar_ready=false
for attempt in $(seq 1 60); do
  if [ "$(curl --silent --show-error --max-time 5 "${SONAR_HOST_URL}/api/system/status" | jq --raw-output '.status // empty')" = "UP" ]; then
    sonar_ready=true
    break
  fi

  echo "Waiting for SonarQube to report UP (${attempt}/60)..."
  sleep 5
done

if [ "${sonar_ready}" != "true" ]; then
  echo "SonarQube did not report UP within five minutes." >&2
  exit 1
fi

dotnet tool restore
dotnet format RfidOperationsConsole.slnx --verify-no-changes --severity info --no-restore

dotnet sonarscanner begin \
  /k:"rfid-operations-console-devsecops-lab" \
  /n:"RFID Operations Console DevSecOps Lab" \
  /v:"${RFID_OPS_VERSION}" \
  /d:sonar.host.url="${SONAR_HOST_URL}" \
  /d:sonar.token="${SONAR_TOKEN}" \
  /d:sonar.qualitygate.wait=true \
  /d:sonar.qualitygate.timeout=300 \
  /d:sonar.cs.opencover.reportsPaths="artifacts/quality/**/coverage.opencover.xml" \
  /d:sonar.coverage.exclusions="src/RfidOps.Api/wwwroot/**" \
  /d:sonar.exclusions="**/bin/**,**/obj/**,artifacts/**,docs/evidence/**,.pipeline/**,src/RfidOps.Api/wwwroot/**"

dotnet build RfidOperationsConsole.slnx \
  --configuration Release \
  --no-incremental

dotnet test RfidOperationsConsole.slnx \
  --configuration Release \
  --no-build \
  --collect:"XPlat Code Coverage" \
  --results-directory artifacts/quality \
  -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=opencover

dotnet sonarscanner end /d:sonar.token="${SONAR_TOKEN}"
