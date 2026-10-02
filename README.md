# RFID Operations Console

A .NET 10 application for registering RFID tags, processing access requests and reviewing decisions in a web console. Active registered tags are granted access; unknown or inactive tags are denied. SQLite stores each decision and queues an outbox entry for downstream delivery.

This project was built for SIT223 7.3HD to exercise a seven-stage Jenkins pipeline. It uses simulated reader events and separate local staging and production-like containers. No physical RFID reader is connected.

[Watch the demonstration](https://drive.google.com/file/d/1IiOHtabuf0zDHN0I_kxpE-9BG_8kJgIL/view?usp=drivesdk) · [Jenkinsfile](Jenkinsfile) · [Build results](docs/evidence/README.md)

![RFID access decision console after build 19](docs/evidence/build-19/visual/released-application.png)

## Pipeline

Jenkins build 19 completed in 4 minutes 24 seconds on 2 October 2026. It built and released image `rfid-ops:1.0.19-f896577` from source revision `f896577ef3b4d88ac5d9b33bd89a48254f52b3f8` and published annotated tag [`v1.0.19-f896577`](https://github.com/stefan-mcf/rfid-operations-console-devsecops-lab/tree/v1.0.19-f896577).

| Stage | What it does | Build 19 result |
| --- | --- | --- |
| Build | Builds a multi-stage Docker image and records its version and image ID | Version and image ID recorded |
| Test | Runs xUnit unit, SQLite persistence and HTTP API tests | 18 passed, 0 failed, 0 skipped |
| Code Quality | Checks formatting and runs SonarQube analysis | Gate passed; 77.0% coverage, 0.0% duplication, 0 new issues |
| Security | Runs NuGet audit and Trivy filesystem and image scans | No findings in the configured high/critical result sets |
| Deploy | Starts the staging container and checks health, metrics and an access request | Healthy; request granted |
| Release | Exercises automatic rollback, then promotes the same image to the production-like environment | Controlled health-check failure restored the previous image; fresh promotion and smoke tests passed; Git tag verified remotely |
| Monitoring | Stops the released app, checks the outage alert, then restarts it | Local firing and resolved alerts received; target recovered |

The [build results](docs/evidence/README.md) include logs, test reports, scan output, rollback and tag receipts, and screenshots. [Build 14](docs/evidence/build-14/README.md) remains available as historical evidence.

## Run locally

Install the .NET 10 SDK specified in [global.json](global.json) and Docker Desktop, then clone the repository:

```bash
git clone https://github.com/stefan-mcf/rfid-operations-console-devsecops-lab.git
cd rfid-operations-console-devsecops-lab
cp .env.example .env
```

Replace the reader and administrator keys in `.env` with two different random values. Load the environment and run the tests and application:

```bash
set -a
source .env
set +a

dotnet restore RfidOperationsConsole.slnx
dotnet test RfidOperationsConsole.slnx
docker build -t "$RFID_OPS_IMAGE" .
docker compose -f deploy/compose.staging.yml up -d
```

Open <http://localhost:18080>. Use the administrator key to register a tag, then the reader key to send an event. The console shows the decision and outbox status. `.env.example` sets `RFID_OPS_IMAGE=rfid-ops:local`; keep that value or choose your own local image tag.

Stop the application with `docker compose -f deploy/compose.staging.yml down`. The named SQLite volume is retained.

For the complete pipeline, follow the [Jenkins and SonarQube setup](infra/jenkins/README.md).

## API

| Route | Purpose | Key |
| --- | --- | --- |
| `GET /health` | Application health and SQLite reachability | None |
| `GET /metrics` | Prometheus metrics | None |
| `GET /api/v1/status` | Tag, event and outbox counts | None |
| `GET /api/v1/events` | Recent access decisions | None |
| `PUT /api/v1/tags/{tagId}` | Register or update a tag | `X-Admin-Key` |
| `POST /api/v1/events` | Process a reader event | `X-Reader-Key` |
| `POST /api/v1/outbox/{eventId}/acknowledge` | Acknowledge downstream delivery | `X-Admin-Key` |

## Project structure

- `src/RfidOps.Core`: access rules and domain types.
- `src/RfidOps.Api`: HTTP routes, SQLite storage and web console.
- `tests/RfidOps.Tests`: unit, persistence and API tests.
- `scripts/ci`: scripts called by the Jenkinsfile.
- `deploy`: staging and production-like Docker Compose configurations.
- `monitoring`: Prometheus, Alertmanager, Grafana and a local alert receiver.
- `infra/jenkins`: Jenkins controller image and Configuration as Code.

## Limitations

SonarQube excludes the browser files in `wwwroot` from analysis and coverage. The gate requires at least 70% coverage, no more than 3% duplication, zero new issues and zero blocker issues. Build 19's new-code comparison uses version `1.0.18-f896577`, which has the same source commit. One minor Dockerfile maintainability issue remains.

Trivy checks high and critical findings; a passing result does not cover every severity. The [security findings](docs/evidence/security-findings.md) explain the SQLite dependency and container-user issues addressed during development.

Build 19 exercised automatic rollback through a controlled local health-check failure, restored the previous image by its immutable ID and passed smoke checks before releasing the candidate normally. Monitoring sends alerts to a local receiver, with no email, SMS or chat integration. The outbox supports acknowledgement, but no external delivery adapter is connected.
