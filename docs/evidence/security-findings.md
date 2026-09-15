# Security findings

Two issues were addressed during development. Jenkins build 14 repeated the NuGet and Trivy checks; [scan output](build-14/artifacts/security/) is retained with the build. Trivy results cover the configured high and critical severities.

## SEC-001 - Vulnerable transitive SQLite native library

- **Detected by:** `dotnet restore RfidOperationsConsole.slnx` with NuGet audit and warnings treated as errors.
- **First observed:** 13 August 2026.
- **Dependency:** `SQLitePCLRaw.lib.e_sqlite3` 2.1.11, introduced transitively by `Microsoft.Data.Sqlite` 10.0.5.
- **Advisory:** `GHSA-2m69-gcr7-jv3q` / `CVE-2025-6965`.
- **Severity:** High, CVSS 7.2 in the reviewed GitHub advisory.
- **Issue:** SQLite versions before 3.50.2 may allow aggregate terms to exceed available columns and cause memory corruption.
- **Treatment:** The native library is explicitly pinned to `SQLitePCLRaw.lib.e_sqlite3` 3.53.3, whose package version identifies SQLite 3.53.3. No warning suppression or false-positive exclusion was added.
- **Local verification:** Clean restore/build/test and final NuGet/Trivy scans succeeded with no remaining high or critical findings.
- **Pipeline verification:** Jenkins build #14 completed the filesystem and image scans. Both Trivy JSON reports contain zero vulnerabilities, misconfigurations, or secrets in the configured high/critical result sets.

Sources:

- <https://github.com/advisories/GHSA-2m69-gcr7-jv3q>
- <https://www.nuget.org/packages/SQLitePCLRaw.lib.e_sqlite3/3.53.3>

## SEC-002 - Monitoring images defaulted to root

- **Detected by:** Trivy misconfiguration scan of the repository filesystem.
- **First observed:** 13 August 2026.
- **Location:** `monitoring/Dockerfile`.
- **Severity:** High (`DS002`).
- **Issue:** The initial Prometheus, Alertmanager and Grafana stages did not explicitly declare non-root runtime users.
- **Treatment:** The runtime stages declare non-root users (`nobody` for Prometheus/Alertmanager and UID `472` for Grafana).
- **Local verification:** The final Trivy filesystem scan reported no remaining high or critical findings, and all three monitoring containers passed their health checks.
- **Pipeline verification:** Jenkins build #14 completed the filesystem scan with zero remaining vulnerabilities, misconfigurations, or secrets in the configured high/critical result sets.
