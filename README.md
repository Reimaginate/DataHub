# DataHub

DataHub is Reimaginate's .NET platform for building, operating and testing
data integration and synchronisation solutions.

This repository contains the current public source snapshot for DataHub,
including the runtime framework, client libraries, command-line tools, testing
frameworks and ReferenceHost sample. Release tags identify the corresponding
DataHub version.

## Documentation

The repository contains lightweight guidance for working with the source and
packages. For installation, configuration, concepts, tutorials and operational
guidance, see the
[DataHub documentation](https://docs.reimaginate.online/datahub).

The local [licensing guide](docs/licensing.md) summarises how the repository
licence applies. The root `LICENSE` file is authoritative.

## Repository Layout

- `framework` — DataHub runtime, abstractions, shared models and client
  libraries.
- `cli` — DataHub command-line application and supporting tools.
- `testing` — DataHub test frameworks and unit tests.
- `samples` — ReferenceHost example application.
- `docs` — repository-specific licensing information.

## Build from Source

Install the prerequisites described in the
[DataHub documentation](https://docs.reimaginate.online/datahub), then run:

```powershell
dotnet restore .\Reimaginate.DataHub.slnx
dotnet build .\Reimaginate.DataHub.slnx --configuration Release
dotnet test .\Reimaginate.DataHub.slnx --configuration Release
```

## Support and Feedback

- Paid support, configuration assistance and defect triage are available
  through [support@reimaginate.online](mailto:support@reimaginate.online).
- This repository does not provide support through GitHub Issues or
  Discussions. See [SUPPORT.md](SUPPORT.md).
- Follow [SECURITY.md](SECURITY.md) to report vulnerabilities privately.
- The public repository is a generated source snapshot and does not accept
  external contributions. See [CONTRIBUTING.md](CONTRIBUTING.md).

## License

The DataHub 1.4 Release Line is licensed under the
[Business Source License 1.1](LICENSE), including the Additional Use Grant
for production use for any purpose other than providing or operating a Hosted
Competitive Offering. The SPDX identifier is
`BUSL-1.1`.

The DataHub 1.4 Release Line converts to the MIT licence on 28 July 2028. See the
[licensing guide](docs/licensing.md) for the application boundary;
`LICENSE` is authoritative.
