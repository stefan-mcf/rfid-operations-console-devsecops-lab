FROM docker:29-cli AS docker-cli
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS dotnet-sdk
FROM aquasec/trivy:0.67.2 AS trivy

FROM jenkins/jenkins:lts-jdk21
USER root

COPY --from=docker-cli /usr/local/bin/docker /usr/local/bin/docker
COPY --from=docker-cli /usr/local/libexec/docker/cli-plugins/docker-compose /usr/local/libexec/docker/cli-plugins/docker-compose
COPY --from=dotnet-sdk /usr/share/dotnet /usr/share/dotnet
COPY --from=trivy /usr/local/bin/trivy /usr/local/bin/trivy

RUN apt-get update \
    && apt-get install --yes --no-install-recommends curl jq libicu76 nodejs \
    && rm -rf /var/lib/apt/lists/* \
    && ln -s /usr/share/dotnet/dotnet /usr/local/bin/dotnet

RUN jenkins-plugin-cli --plugins \
    configuration-as-code \
    credentials-binding \
    git \
    job-dsl \
    junit \
    pipeline-stage-view \
    workflow-aggregator

USER jenkins
