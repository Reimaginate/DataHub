# DataHub Licensing

DataHub version 1.4.0 is source-available under the
[Business Source License 1.1](../LICENSE). The SPDX identifier is
`BUSL-1.1`, but the complete root `LICENSE` file is authoritative.

BSL 1.1 is not an Open Source licence before the Change Date. DataHub
version 1.4.0 changes to the MIT License on 28 July 2028.

This guide explains the intended application of the licence; it is not legal
advice.

## Production Use

The Additional Use Grant permits production use for any purpose other than
providing or operating a Hosted Competitive Offering.

The standard BSL 1.1 terms continue to permit copying, modification, creation
of derivative works, redistribution and non-production use. Developing or
evaluating a prospective hosted offering without operating it in production
is therefore permitted.

A Hosted Competitive Offering is a business offering to third parties that
uses DataHub or a derivative on a hosted, managed, cloud or SaaS basis and
either exposes general-purpose integration-platform functionality or uses
that functionality to provide integration services as a primary or material
feature. It can qualify even if it is free or bundled, customers never see a
DataHub interface, or Reimaginate does not yet offer an equivalent service.

General-purpose integration-platform functionality includes selecting or
configuring systems, mappings, transformations, synchronisation rules,
workflows, jobs, connectors or agents, and executing, orchestrating,
monitoring or administering integrations between multiple systems. A mere
API, webhook, import/export facility, fixed integration or product-specific
integration setting does not trigger the restriction by itself.

A Common DataHub Management Service is a common provider-operated hosted
service or control plane that allows customers to monitor, configure,
administer, control or manage their DataHub deployments and is designed,
marketed or supplied for deployments belonging to more than one unrelated
customer. It remains common when deployments and data are segregated, when
white-labelled, when it cannot configure integrations, or when it initially
has only one customer.

## Common Scenarios

This table is a practical summary only. The definitions and conditions in
[`LICENSE`](../LICENSE) control.

| Scenario | Position under the licence |
| --- | --- |
| Development or evaluation of a prospective hosted offering without production use | Permitted as non-production use under BSL 1.1 |
| An organisation uses DataHub for its own operations, including across affiliates | Permitted |
| A product uses DataHub behind the scenes for ancillary, product-specific integrations and is not marketed as an integration platform or managed integration service | Permitted |
| A customer controls its own DataHub deployment and uses it for its operations | Permitted |
| A consultant implements, customises or supports DataHub without providing or operating a competitive hosted offering in production | Permitted |
| An agency-style consultant or MSP operates a logically segregated deployment for one customer's own operations | Permitted |
| Independently developed connectors or extensions interoperate with DataHub without incorporating DataHub code | Permitted |
| A management portal is deployed in one customer's Azure tenant and is used only for that Customer Deployment | Permitted |
| A provider-hosted single-customer portal instance is used only for that Customer Deployment | Permitted |
| Provider-internal tooling accessible only to the provider's personnel supports permitted Customer Deployments | Permitted |
| The same portal software is separately deployed for unrelated customers without a Common DataHub Management Service or another customer-facing common management control plane | Permitted |
| Separately deployed customer portals use shared identity, licensing, billing, update, telemetry, logging or support infrastructure without common customer-facing DataHub management capability | Not restricted on that fact alone |
| A product exposes only an API, webhook, fixed integration, import/export function or product-specific integration settings | Not restricted on that fact alone |
| An independently developed monitoring tool neither uses nor connects to DataHub | Not restricted on that fact alone |
| A white-labelled hosted integration platform uses DataHub or a derivative | Not permitted without a commercial licence |
| A free or bundled hosted platform exposes general-purpose integration functionality | Not permitted without a commercial licence |
| A provider-branded managed integration service uses DataHub to deliver integration services as a primary or material feature | Not permitted without a commercial licence |
| A managed service hides the DataHub interface but operates its integration functionality for customers | Not permitted without a commercial licence |
| A shared customer-facing DataHub management portal serves unrelated customers | Not permitted without a commercial licence |
| A white-labelled multi-customer DataHub management portal uses a common provider-operated service | Not permitted without a commercial licence |
| A centrally operated multi-customer DataHub management control plane uses DataHub | Not permitted without a commercial licence |

## Consultants and Managed Service Providers

The distinction is the service being supplied.

An agency-style provider may operate a Customer Deployment for a specific
customer's own business operations. Shared infrastructure is acceptable when
each customer's access, configuration and data remain logically segregated.
The provider must not market or supply those deployments collectively as its
own integration platform, Common DataHub Management Service or other Hosted
Competitive Offering.

A customer-facing management portal may form part of one Customer Deployment
and may be hosted in the customer's environment or in a separate
provider-hosted instance. The same portal software may be separately deployed
for other customers, provided that the instances are not connected to a
Common DataHub Management Service or another customer-facing common control
plane through which customers can monitor, configure, administer, control or
manage Customer Deployments. Shared identity, licensing, billing, update,
telemetry, logging or support infrastructure does not, by itself, create that
connection.

Tools accessible only to the provider's personnel may be used for ongoing
monitoring, administration, maintenance and support of permitted Customer
Deployments. Neither this permission nor the single-customer portal
permission protects an otherwise prohibited Hosted Competitive Offering,
whether provider-branded or white-labelled.

Professional services do not provide a route around the restriction. They
are permitted only when they are not used to provide or operate a Hosted
Competitive Offering in production.

## Commercial Licensing

If a planned use may be a Hosted Competitive Offering, contact
[support@reimaginate.online](mailto:support@reimaginate.online) before
production use. Reimaginate will review commercial licensing enquiries case
by case.

GitHub Issues and Discussions are not licensing or support channels.

## Release Boundary

This licence applies specifically to DataHub version 1.4.0 and the 11 named
NuGet packages in the root `LICENSE`. It does not automatically apply to
later versions.

The intended first public distribution date is 28 July 2026. If source
publication does not occur on that date, the release tag must not be created
or moved. Reimaginate must choose a new exact Change Date, commit the updated
licence and repeat all validation before publication.

The public source snapshot must exist at the matching annotated `v1.4.0` tag
before any v1.4.0 NuGet package is published.

## NuGet Packages

BSL 1.1 is not declared as a NuGet licence expression. Each v1.4.0 DataHub
package embeds the exact root `LICENSE` file, identifies it through
`PackageLicenseFile` and requires licence acceptance.

Package validation checks the licensed version, exact Change Date, complete
package list, canonical BSL terms and byte equality with the root licence.
The canonical terms are pinned from the
[SPDX BUSL-1.1 text](https://spdx.org/licenses/BUSL-1.1.html) by SHA-256;
only the Parameters and Additional Use Grant are customised.

## Third-Party and Separately Licensed Components

Any Reimaginate component expressly distributed by the Licensor under a
separate licence, and any third-party component identified by the Licensor as
subject to a separate licence, is excluded from the Licensed Work and remains
subject to that separate licence. Those notices must be preserved in source
and package distributions.

## Independent Development

The licence does not restrict competing software independently developed
without copying, modifying, embedding, hosting or otherwise using DataHub.
