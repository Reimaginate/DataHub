# Reimaginate.DataHub.AspNetCore

ASP.NET Core endpoint helpers for hosting DataHub APIs in Azure Container Apps and other web hosts.

Use `MapDataHubClientEndpoint` and `MapDataHubCliEndpoint` to expose DataHub request endpoints with structured diagnostic error responses.

See the [DataHub repository](https://github.com/Reimaginate/DataHub) for current
client and host configuration documentation.

Shared key client authorization:

```csharp
app.MapDataHubClientEndpoint("/api/Client", options =>
{
    options.AuthorizeAsync = (request, _) =>
    {
        var expectedKey = builder.Configuration["x-functions-key"];
        var suppliedKey = request.Headers["x-functions-key"].FirstOrDefault();
        return Task.FromResult(!string.IsNullOrEmpty(expectedKey) && expectedKey == suppliedKey);
    };
});
```

Microsoft Entra client authorization:

```csharp
builder.Services.AddDataHubClientAzureAdAuthorization(options =>
{
    options.TenantId = "<tenant-id>";
    options.Audience = "api://<datahub-api-client-id>";
    options.AllowedClientIds = new[] { "<caller-application-client-id>" };
    options.AllowedObjectIds = new[] { "<managed-identity-object-id>" };
});

app.MapDataHubClientEndpoint("/api/Client", options =>
{
    options.UseAzureAdBearerAuthorization();
});
```

The endpoint rejects delegated user tokens and only accepts app or managed identity tokens whose `azp`, `appid`, or `oid` claim appears in the configured allow-list.

CLI authentication:

```csharp
builder.Services.AddDataHubCliAzureAdAuthentication(options =>
{
    options.TenantId = "<tenant-id>";
    options.Audience = "api://7a3a7b0c-3f0b-43dd-b45f-487f1060ee91";
    options.RequiredScope = "datahub_cli";
});

app.MapDataHubCliEndpoint("/api/CLI", options =>
{
    options.UseAzureAdCliAuthentication();
});
```

The CLI endpoint accepts delegated Microsoft Entra user tokens only. It rejects app-only tokens, requires the configured `datahub_cli` scope, resolves the token to a registered DataHub user by tenant plus Entra object id, and then relies on DataHub roles and permissions for command authorization. The standard DataHub CLI path uses the shared Reimaginate/DataHub app registration, so customers do not create their own app registration for CLI sign-in; tenant admin consent may still be required.

Customer administrators sign in through the DataHub CLI. The CLI uses MSAL to request the delegated API scope:

```powershell
datahub profiles set-target datahub --profile <name> --url <datahub-cli-url> --tenant <tenant-id>
datahub login --profile <name>
```

## Support

Paid support, configuration assistance and defect triage are available through
[support@reimaginate.online](mailto:support@reimaginate.online).

GitHub Issues and Discussions are not support channels.
