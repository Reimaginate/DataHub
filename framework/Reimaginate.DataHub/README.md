# Reimaginate.DataHub

Core DataHub runtime package for request handling, data operations, validation, telemetry, and persistence integration.

Use this package in applications that host or compose DataHub runtime behavior.

## Tracking rebases

Source and Data Hub rebases coordinate with existing entity locks in batches of
500. Cosmos transactional checkpoints replace baselines and remove included history
atomically per entity. Results report confirmed progress if later work fails.

Current Data Hub history can recover from a stored entity with a
`STORED_ENTITY_RECOVERY` warning. This preserves stored values but can discard
changes present only in damaged tracking history. Historical rebases preserve
history when recovery cannot safely represent the requested cutoff. Source rebases
retain a failing entry and its later history while compacting the valid prefix.

Set `IncludeArchivedTrackingEntries` to `false` for compact responses with
`ArchivedTrackingEntryCount`. The default is `true` for existing clients. Shared
lock storage is required to coordinate separate processes. Incoming merge
processing is unchanged.

For current documentation and release information, visit the
[DataHub repository](https://github.com/Reimaginate/DataHub).

## Support

Paid support, configuration assistance and defect triage are available through
[support@reimaginate.online](mailto:support@reimaginate.online).

GitHub Issues and Discussions are not support channels.
