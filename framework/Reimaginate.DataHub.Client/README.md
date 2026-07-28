# Reimaginate.DataHub.Client

HTTP client components for calling DataHub APIs with the shared DataHub model packages.

This package is intended for services that need to integrate with DataHub over its client-facing API surface.

## Authentication

Supported client authentication modes:

- `SharedKey`: sends the existing `x-functions-key` header and remains the default.
- `ApplicationRegistration`: uses a Microsoft Entra application registration and sends a bearer token.
- `ManagedIdentity`: uses a system-assigned or user-assigned managed identity and sends a bearer token.

See the [DataHub repository](https://github.com/Reimaginate/DataHub) for current
configuration, host validation, appsettings examples and environment-variable
documentation.

Shared key:

```csharp
services.AddDataHubClient(options => options.WithSharedKey(
    "https://datahub.example.com/api/Client",
    "<shared-key>"));
```

Application registration:

```csharp
services.AddDataHubClient(options => options.WithApplicationRegistration(
    "https://datahub.example.com/api/Client",
    "api://<datahub-api-client-id>/.default",
    "<tenant-id>",
    "<caller-application-client-id>",
    "<caller-application-client-secret>"));
```

Managed identity:

```csharp
services.AddDataHubClient(options => options.WithManagedIdentity(
    "https://datahub.example.com/api/Client",
    "api://<datahub-api-client-id>/.default",
    "<managed-identity-client-id>"));
```

## Support

Paid support, configuration assistance and defect triage are available through
[support@reimaginate.online](mailto:support@reimaginate.online).

GitHub Issues and Discussions are not support channels.
