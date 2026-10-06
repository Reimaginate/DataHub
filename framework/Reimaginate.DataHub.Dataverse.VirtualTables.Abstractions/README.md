# Reimaginate.DataHub.Dataverse.VirtualTables.Abstractions

Abstractions for building DataHub Dataverse virtual table integrations.

This package provides shared contracts used by Dataverse virtual table request and handler implementations.

## Migrating from earlier package names

This package replaces `Reimaginate.DataHub.VirtualEntities.Abstractions` and
`Reimaginate.DataHub.VirtualTables.Abstractions`. The Dataverse-qualified name
identifies the integration these contracts support.

Replace either old package reference with
`Reimaginate.DataHub.Dataverse.VirtualTables.Abstractions` and update `using`
directives and fully qualified type names from either old namespace prefix to
`Reimaginate.DataHub.Dataverse.VirtualTables.Abstractions.*`.

The assembly name also changes to
`Reimaginate.DataHub.Dataverse.VirtualTables.Abstractions`.
Existing contract type names, including `GetVirtualEntitiesRequest`,
`GetVirtualEntitiesResponse` and `VirtualEntityQueryExpression`, are unchanged.
Rebuild consumers against the replacement package.

For current documentation and release information, visit the
[DataHub repository](https://github.com/Reimaginate/DataHub).

## Support

Paid support, configuration assistance and defect triage are available through
[support@reimaginate.online](mailto:support@reimaginate.online).

GitHub Issues and Discussions are not support channels.
