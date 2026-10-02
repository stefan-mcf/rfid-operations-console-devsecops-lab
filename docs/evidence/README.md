# Build results

Jenkins build 19 passed all seven pipeline stages on 2 October 2026. The run took 264,234 ms and archived 42 artifacts. Its source revision was `f896577ef3b4d88ac5d9b33bd89a48254f52b3f8`.

| Stage | Result | Files |
| --- | --- | --- |
| Build | Version `1.0.19-f896577` and immutable image ID | [Build manifest](build-19/artifacts/build-manifest.json), [image inspection](build-19/artifacts/build-image-inspect.json) |
| Test | 18 passed; no failures, errors or skips | [JUnit report](build-19/artifacts/test-results/RfidOps.Tests-test-result.xml), [coverage](build-19/artifacts/quality/) |
| Code Quality | Gate OK; 77.0% coverage, 0.0% duplication, zero new or blocker issues | [Quality gate](build-19/platform/sonar-quality-gate.json), [analysis identity](build-19/platform/sonar-readback-identity.json), [open issues](build-19/platform/sonar-open-issues.json) |
| Security | No findings in the high/critical scan sets | [NuGet and Trivy output](build-19/artifacts/security/), [development findings](security-findings.md) |
| Deploy | Staging healthy; registered tag granted access | [Deployment output](build-19/artifacts/deploy/) |
| Release | Controlled candidate failure automatically restored the previous image; fresh promotion and smoke checks passed; annotated tag verified remotely | [Rollback drill](build-19/artifacts/rollback-drill/drill-manifest.json), [restored image](build-19/artifacts/rollback-drill/rollback/rollback-manifest.json), [release manifest](build-19/artifacts/release/release-manifest.json), [tag receipt](build-19/artifacts/release/git-tag.json), [remote readback](build-19/platform/git-tag-readback.json) |
| Monitoring | Outage detected, firing and resolved alerts delivered locally, application recovered | [Monitoring output](build-19/artifacts/monitoring/), [delivery readback](build-19/platform/local-alert-delivery-readback.json) |

[Jenkins metadata](build-19/platform/jenkins-build.json), [stage timings](build-19/platform/jenkins-workflow.json) and the [console log](build-19/platform/jenkins-console.txt) identify the run. The [screenshots](build-19/visual/) show Jenkins, rollback and release output, the Git tag and the released application.

The rollback drill stopped the candidate to produce a real failed health check. The release failure handler restored image `sha256:5da899f91ebf789351faf22091cf81dc754891eed93e88f1ca555abdf58f2a4c`, version `1.0.17-341e4fa`, and verified health and access processing. The subsequent normal release promoted the build 19 image unchanged and published [`v1.0.19-f896577`](https://github.com/stefan-mcf/rfid-operations-console-devsecops-lab/tree/v1.0.19-f896577).

The receiver log archived by Jenkins includes the firing delivery. The separate [post-run readback](build-19/platform/local-alert-delivery-readback.json) records both firing and resolved deliveries for the same incident, `2026-10-02T08:20:49.936Z`, and the checksum of the preserved [receiver log](build-19/platform/local-alert-receiver-post-run.log).

Browser files are excluded from SonarQube analysis and coverage. One minor Dockerfile issue remains. The zero-new-issue result compares against configured previous version `1.0.18-f896577`, with the same source commit. Deployment, rollback and alerts ran locally with simulated tag events.

[Build 14](build-14/README.md) preserves the original 11 September run, its 28 archived artifacts and screenshots. That run did not exercise rollback.

[Watch the demonstration](https://drive.google.com/file/d/1IiOHtabuf0zDHN0I_kxpE-9BG_8kJgIL/view?usp=drivesdk).
