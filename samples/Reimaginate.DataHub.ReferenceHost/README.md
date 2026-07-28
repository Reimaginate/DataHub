# Reimaginate.DataHub.ReferenceHost

Reference ASP.NET Core host for running DataHub in Azure Container Apps.

This sample demonstrates the supported host pattern:

- `GET /healthz` for container health checks.
- `GET /readyz` for cached deployment readiness checks against configured dependencies.
- `POST /api/Client` mapped with `MapDataHubClientEndpoint`.
- `POST /api/CLI` mapped with `MapDataHubCliEndpoint`.
- Structured diagnostic error responses for unauthorized, invalid, validation, and server failures.
- Azure Monitor OpenTelemetry wiring with low-volume production defaults.

## Run Locally

```powershell
dotnet run --project .\samples\Reimaginate.DataHub.ReferenceHost\Reimaginate.DataHub.ReferenceHost.csproj
```

The development key is `development-key`. The health endpoint and invalid request diagnostics can be exercised without a live Cosmos DB. Real DataHub request handling still requires valid DataHub storage configuration.

## Configuration

The checked-in settings contain only blank or development placeholders. Use
environment variables or a managed secret store for deployment credentials;
never add secrets to the settings files or container image.

The sample supports connection-string, application-registration and managed
identity access to Cosmos DB, shared-key or Microsoft Entra client
authentication, Microsoft Entra CLI authentication, Redis locking and Azure
Monitor telemetry.

See the [DataHub documentation](https://docs.reimaginate.online/datahub) for
configuration, authentication, Cosmos DB permissions, observability and
deployment guidance.

## Azure Container Apps

Build the included Dockerfile and configure secrets and environment variables
in the Container App. Do not bake connection strings or keys into the image.
