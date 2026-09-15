# Security policy

The application simulates RFID access requests on local infrastructure.

## Reporting

Do not open a public issue containing credentials or client information. Report a suspected vulnerability privately to the repository owner.

## Secret handling

- Generate distinct reader and administrator keys for each local environment.
- Keep keys in local environment variables or Jenkins credentials.
- Never commit `.env`, customer identifiers, production endpoints or exported provider configuration.
- Rotate a key immediately if it appears in source, logs, screenshots or evidence.

## Scan handling

The Security stage runs NuGet audit and Trivy filesystem and image scans. Trivy blocks high and critical findings. Scan reports are stored as pipeline artifacts; the [findings record](docs/evidence/security-findings.md) describes the issues addressed during development.
