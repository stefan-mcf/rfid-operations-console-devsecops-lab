#!/usr/bin/env bash
source "$(dirname "$0")/common.sh"

rm -rf artifacts/test-results .sonarqube
mkdir -p artifacts/test-results

node --check src/RfidOps.Api/wwwroot/app.js

dotnet restore RfidOperationsConsole.slnx
dotnet test RfidOperationsConsole.slnx \
  --configuration Release \
  --no-restore \
  --logger:"junit;LogFilePath=${project_root}/artifacts/test-results/{assembly}-test-result.xml;MethodFormat=Class;FailureBodyFormat=Verbose" \
  --collect:"XPlat Code Coverage" \
  --results-directory artifacts/test-results
