# Security findings

Two issues were fixed during development. Jenkins build 14 repeated the NuGet and Trivy checks. The [scan reports](build-14/artifacts/security/) cover high and critical Trivy findings.

## SQLite native library

NuGet audit stopped `dotnet restore RfidOperationsConsole.slnx` on 13 August 2026 because audit warnings are treated as errors. `Microsoft.Data.Sqlite` 10.0.5 brought in `SQLitePCLRaw.lib.e_sqlite3` 2.1.11 as a transitive dependency.

The advisory, `GHSA-2m69-gcr7-jv3q` / `CVE-2025-6965`, was rated High with a CVSS score of 7.2. SQLite versions before 3.50.2 may allow aggregate terms to exceed available columns and cause memory corruption.

The project pins `SQLitePCLRaw.lib.e_sqlite3` to 3.53.3, which contains SQLite 3.53.3. No warning suppression or false-positive exclusion was added. Restore, build and tests then passed, and the final local scans found no high or critical issues. Build 14's Trivy filesystem and image reports also contain no vulnerabilities, misconfigurations or secrets in those severity levels.

Sources:

- <https://github.com/advisories/GHSA-2m69-gcr7-jv3q>
- <https://www.nuget.org/packages/SQLitePCLRaw.lib.e_sqlite3/3.53.3>

## Monitoring container users

Trivy reported High-severity `DS002` findings in `monitoring/Dockerfile` on 13 August 2026. The Prometheus, Alertmanager and Grafana stages lacked explicit non-root runtime users.

The stages now declare `nobody` for Prometheus and Alertmanager, and UID `472` for Grafana. All three containers passed their health checks after the change. The final local scan and build 14's filesystem scan reported no remaining high or critical findings.
