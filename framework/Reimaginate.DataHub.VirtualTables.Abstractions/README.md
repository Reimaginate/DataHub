# Reimaginate.DataHub.VirtualTables.Abstractions

Abstractions for building DataHub virtual table integrations.

This package provides shared contracts used by virtual table request and handler implementations.

## Migrating from VirtualEntities.Abstractions

This package replaces `Reimaginate.DataHub.VirtualEntities.Abstractions`.
Replace the old package reference with `Reimaginate.DataHub.VirtualTables.Abstractions`
and update `using` directives and fully qualified type names from
`Reimaginate.DataHub.VirtualEntities.Abstractions.*` to
`Reimaginate.DataHub.VirtualTables.Abstractions.*`.

The assembly name also changes to `Reimaginate.DataHub.VirtualTables.Abstractions`.
Existing contract type names, including `GetVirtualEntitiesRequest`,
`GetVirtualEntitiesResponse` and `VirtualEntityQueryExpression`, are unchanged.
Rebuild consumers against the replacement package.

For current documentation and release information, visit the
[DataHub repository](https://github.com/Reimaginate/DataHub).

## Support

Paid support, configuration assistance and defect triage are available through
[support@reimaginate.online](mailto:support@reimaginate.online).

GitHub Issues and Discussions are not support channels.
