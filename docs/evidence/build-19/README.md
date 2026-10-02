# Jenkins build 19

Build 19 passed all seven pipeline stages on 2 October 2026 in 264,234 ms and archived 42 artifacts. It built version `1.0.19-f896577` from revision `f896577ef3b4d88ac5d9b33bd89a48254f52b3f8`.

- [artifacts/](artifacts/): the unchanged Jenkins archive, including build, test, scan, staging, rollback drill, release and monitoring outputs.
- [platform/](platform/): Jenkins and SonarQube API responses, console output, remote Git tag verification and alert-delivery readback.
- [visual/](visual/): native captures of Jenkins, release output, the Git tag and the released application.

The [build manifest](artifacts/build-manifest.json) and [release manifest](artifacts/release/release-manifest.json) record the same image ID, `sha256:b4739c206cecdb6934cc5d07ec1fc7e8d2865819e65eb5e3b06f010668489434`. [Jenkins metadata](platform/jenkins-build.json) and [stage results](platform/jenkins-workflow.json) record the outcome and timings.

The [rollback drill](artifacts/rollback-drill/drill-manifest.json) stopped the candidate and observed its failed health check. The failed request produced empty `health-after-stop.json` output and a preserved [curl error](artifacts/rollback-drill/health-after-stop.stderr). Automatic rollback restored the [previous immutable image](artifacts/rollback-drill/rollback/rollback-manifest.json) and passed health and access smoke checks. A subsequent normal release promoted the candidate and published the annotated [Git tag](artifacts/release/git-tag.json); the [remote readback](platform/git-tag-readback.json) confirms that tag points to the source revision.

The [Sonar identity receipt](platform/sonar-readback-identity.json) associates the successful analysis and its quality gate with this exact version and commit. Its zero-new-issue comparison is against `1.0.18-f896577`, with the same source commit. [Measures](platform/sonar-measures.json) show 77.0% coverage and 0.0% duplication; one minor Dockerfile issue remains.

The archived receiver log contains the firing delivery. The later [receiver log](platform/local-alert-receiver-post-run.log) and [delivery readback](platform/local-alert-delivery-readback.json) preserve both firing and resolved events for incident `2026-10-02T08:20:49.936Z`. The readback records receipt timestamps and a checksum; it does not replace the Jenkins archive. The archived [recovery target](artifacts/monitoring/targets-after-recovery.json) is up and [remaining alerts](artifacts/monitoring/alerts-after-recovery.json) are empty.

See the [results index](../README.md) for the stage summary and [build 14](../build-14/README.md) for historical evidence. All deployment and failure demonstrations use the local environment and simulated RFID events.
