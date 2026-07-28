# Reimaginate.DataHub.CLI

Standalone .NET global tool for administering DataHub instances.

For installation, authentication, profiles and the complete command reference,
see the [DataHub repository](https://github.com/Reimaginate/DataHub).

## Install

```powershell
dotnet tool install --global Reimaginate.DataHub.CLI
```

The executable command is `datahub`.

## Quick Start

```powershell
datahub profiles add dev
datahub profiles set-target datahub --profile dev --url <datahub-cli-url> --tenant <tenant-id>
datahub profiles use dev
datahub login --profile dev
datahub entities list "x.entityType == 'contact'"
```

The CLI uses Microsoft Entra delegated-user authentication. Profiles store
target configuration; they do not store credentials.

## Support

Paid support, configuration assistance and defect triage are available through
[support@reimaginate.online](mailto:support@reimaginate.online).

GitHub Issues and Discussions are not support channels.
