# Build results

Jenkins build 14 passed all seven pipeline stages on 11 September 2026. The run took 195,162 ms and archived 28 artifacts. Its source revision was `a1152b51f98989d0e4052b4e3f677a9c561cb47e`.

| Stage | Result | Files |
| --- | --- | --- |
| Build | Version `1.0.14-a1152b5` and immutable image ID | [Build manifest](build-14/artifacts/build-manifest.json), [image inspection](build-14/artifacts/build-image-inspect.json) |
| Test | 18 passed; no failures, errors or skips | [JUnit report](build-14/artifacts/test-results/RfidOps.Tests-test-result.xml), [coverage](build-14/artifacts/quality/) |
| Code Quality | Gate OK; 77.0% coverage, 0.0% duplication, zero new or blocker issues | [Quality gate](build-14/platform/sonar-quality-gate.json), [open issues](build-14/platform/sonar-open-issues.json) |
| Security | No findings in the high/critical scan sets | [NuGet and Trivy output](build-14/artifacts/security/), [findings and fixes](security-findings.md) |
| Deploy | Staging healthy; registered tag granted access | [Deployment output](build-14/artifacts/deploy/) |
| Release | Same image promoted; health and smoke checks passed | [Release output](build-14/artifacts/release/) |
| Monitoring | Outage detected, local alert delivered, application recovered | [Monitoring output](build-14/artifacts/monitoring/), [delivery log](build-14/platform/local-alert-receiver-post-run.log) |

[Jenkins metadata](build-14/platform/jenkins-build.json), [stage timings](build-14/platform/jenkins-workflow.json) and the [console log](build-14/platform/jenkins-console.txt) identify the run. The [screenshots](build-14/visual/) show Jenkins, Grafana and the released application.

The receiver log archived by Jenkins contains startup messages only. The separate [post-run readback](build-14/platform/local-alert-delivery-readback.json) records the firing and resolved deliveries and the checksum of the corresponding log.

Browser files are excluded from SonarQube analysis and coverage. One minor Dockerfile issue remains. Deployment and alerts ran locally with simulated tag events; rollback was not exercised.

The [source record](../PROVENANCE.md) explains how these results relate to the published source files. [Watch the demonstration (7:20)](https://drive.google.com/file/d/1IiOHtabuf0zDHN0I_kxpE-9BG_8kJgIL/view?usp=drivesdk).
