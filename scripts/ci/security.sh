#!/usr/bin/env bash
source "$(dirname "$0")/common.sh"

require_value RFID_OPS_VERSION
image_repository="${RFID_OPS_IMAGE_REPOSITORY:-rfid-ops}"
image="${image_repository}:${RFID_OPS_VERSION}"
image_archive=".pipeline/security/${RFID_OPS_VERSION}.tar"

rm -rf artifacts/security
mkdir -p artifacts/security .pipeline/security
trap 'rm -f "$image_archive"' EXIT

dotnet list RfidOperationsConsole.slnx package \
  --vulnerable \
  --include-transitive \
  --format json \
  --output-version 1 \
  > artifacts/security/dotnet-vulnerabilities.json

trivy fs \
  --scanners vuln,misconfig,secret \
  --severity HIGH,CRITICAL \
  --exit-code 1 \
  --skip-dirs .git \
  --skip-dirs .pipeline \
  --skip-dirs artifacts \
  --format json \
  --output artifacts/security/trivy-filesystem.json \
  .

docker save --output "$image_archive" "$image"
trivy image \
  --scanners vuln,misconfig,secret \
  --severity HIGH,CRITICAL \
  --exit-code 1 \
  --format json \
  --output artifacts/security/trivy-image.json \
  --input "$image_archive"
