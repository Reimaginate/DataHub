using System.Collections.Generic;
using Reimaginate.DataHub.SharedModels.Core;

namespace Reimaginate.DataHub.SharedModels.Requests.Client;

public class ResolveEntityReferencesRequest : DataHubClientRequest<ResolveEntityReferencesResponse>
{
    public ResolveEntityReferencesRequest()
    {
        RequestType = nameof(ResolveEntityReferencesRequest);
    }
    public List<ExternalEntityReference> EntityReferences { get; set; } = new();
}