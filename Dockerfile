FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source

COPY global.json Directory.Build.props RfidOperationsConsole.slnx ./
COPY src/RfidOps.Core/RfidOps.Core.csproj src/RfidOps.Core/
COPY src/RfidOps.Api/RfidOps.Api.csproj src/RfidOps.Api/
COPY tests/RfidOps.Tests/RfidOps.Tests.csproj tests/RfidOps.Tests/
RUN dotnet restore RfidOperationsConsole.slnx

COPY src/ src/
RUN dotnet publish src/RfidOps.Api/RfidOps.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
ARG APP_VERSION=dev
ARG VCS_REF=unknown
LABEL org.opencontainers.image.title="RFID Operations Console DevSecOps Lab" \
      org.opencontainers.image.version="${APP_VERSION}" \
      org.opencontainers.image.revision="${VCS_REF}" \
      org.opencontainers.image.description="Client-neutral RFID access-decision simulator"

WORKDIR /app
RUN mkdir -p /app/data && chown -R app:app /app
COPY --from=build --chown=app:app /app/publish .

ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_EnableDiagnostics=0 \
    RFID_OPS_DB_PATH=/app/data/rfid-ops.db
EXPOSE 8080
USER app

HEALTHCHECK --interval=10s --timeout=4s --start-period=10s --retries=5 \
  CMD ["dotnet", "RfidOps.Api.dll", "--health-check"]

ENTRYPOINT ["dotnet", "RfidOps.Api.dll"]
