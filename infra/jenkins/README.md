# Jenkins and SonarQube setup

The local stack runs Jenkins at <http://localhost:28082> and SonarQube at <http://localhost:29000>. Jenkins mounts the host Docker socket to build and deploy containers. Keep the controller private and run only trusted jobs.

The supplied [Configuration as Code](casc/jenkins.yml) creates the administrator account, credentials and `rfid-operations-console` pipeline job. The job reads the repository's `main` branch and root `Jenkinsfile`. Builds are started manually.

## Configure the services

From the repository root:

```bash
cp infra/jenkins/jenkins.env.example infra/jenkins/.env.jenkins
```

Set the administrator password, reader and administrator keys, Grafana password and GitHub read token in the copied file. The checked-in Jenkins configuration retains a GitHub credential; use a token that can read this repository. Do not commit the environment file.

Start SonarQube first so you can create its project and token:

```bash
docker compose -f infra/jenkins/compose.jenkins.yml \
  --env-file infra/jenkins/.env.jenkins up -d sonarqube
```

Open SonarQube, complete its administrator setup, and create project `rfid-operations-console-devsecops-lab`. Generate an analysis token and put it in `SONAR_TOKEN` in `.env.jenkins`.

Assign a quality gate with these failure conditions:

| Metric | Fail when |
| --- | --- |
| Overall coverage | Below 70% |
| Duplicated lines | Above 3% |
| New issues | Above 0 |
| Blocker issues | Above 0 |

Use the previous version for the new-code period. [Build 14's gate response](../../docs/evidence/build-14/platform/sonar-quality-gate.json) records the metric keys and thresholds. The analysis script excludes `wwwroot` from analysis and coverage, and generated outputs and archived evidence from source analysis.

Load the environment and start Jenkins:

```bash
export DOCKER_SOCKET_PATH="$(docker context inspect --format '{{.Endpoints.docker.Host}}' | sed 's#^unix://##')"
set -a
source infra/jenkins/.env.jenkins
set +a
docker compose -f infra/jenkins/compose.jenkins.yml up -d --build
```

The socket command assumes a local Docker context using a Unix socket. The supplied stack uses persistent volumes for Jenkins and SonarQube; changing an environment value does not reset existing service accounts.

## Run the pipeline

Sign in to Jenkins with the configured administrator account and open `rfid-operations-console`. Its credentials are:

| ID | Use |
| --- | --- |
| `github-coursework-token` | Repository checkout |
| `rfid-ops-reader-key` | Reader-event smoke tests |
| `rfid-ops-admin-key` | Tag registration |
| `sonar-token` | SonarQube analysis |
| `grafana-admin-password` | Grafana administration |

Select **Build Now**. Jenkins builds the image, tests it, runs quality and security checks, deploys to staging, promotes the same image and tests monitoring. A failed gate stops later stages. Test reports and stage outputs are archived with the build.

The monitoring stage deliberately stops and restarts the released application. Alertmanager sends firing and resolved alerts to a local receiver. No external notification service is configured. Jenkins removes the staging environment after each run; the released application and monitoring services remain available.
