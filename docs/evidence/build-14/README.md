# Jenkins build 14

Build 14 passed all seven stages in 195,162 ms and archived 28 artifacts. It built version `1.0.14-a1152b5` from revision `a1152b51f98989d0e4052b4e3f677a9c561cb47e`.

- [artifacts/](artifacts/): the collected Jenkins archive, including build, test, scan, deployment, release and monitoring outputs.
- [platform/](platform/): Jenkins and SonarQube API responses, console output and the alert-delivery readback.
- [visual/](visual/): screenshots of Jenkins, Grafana and the released console.

The [build manifest](artifacts/build-manifest.json) records the image ID. [Jenkins metadata](platform/jenkins-build.json) and [stage results](platform/jenkins-workflow.json) record the outcome and timings. See the [results index](../README.md) for a stage-by-stage summary.

The archived receiver log was collected before delivery appeared and contains startup messages only. [local-alert-receiver-post-run.log](platform/local-alert-receiver-post-run.log) contains the later firing and resolved deliveries. [local-alert-delivery-readback.json](platform/local-alert-delivery-readback.json) records its checksum and matches the alert's `startsAt` value to the incident.

These logs and screenshots are unchanged from the recorded run. The [source record](../../PROVENANCE.md) maps the published application and pipeline files to its original source revision.
