# Reimaginate.DataHub.CLI.Tools

Reusable DataHub CLI commands and supporting services for Reimaginate CLI hosts.

Use this package when an existing CLI host needs to add the DataHub command
tree without packaging the standalone `datahub` global tool.

For host integration, authentication and command reference documentation, see
the [DataHub repository](https://github.com/Reimaginate/DataHub).

## Host Integration

Register services and add the command tree:

```csharp
services.AddDataHubCliCommands(configuration);
rootCommand.AddDataHubTools(serviceProvider);
```

The package uses Microsoft Entra delegated-user authentication. Profiles store
target metadata; access tokens are not persisted in profile files.

## Support

Paid support, configuration assistance and defect triage are available through
[support@reimaginate.online](mailto:support@reimaginate.online).

GitHub Issues and Discussions are not support channels.
