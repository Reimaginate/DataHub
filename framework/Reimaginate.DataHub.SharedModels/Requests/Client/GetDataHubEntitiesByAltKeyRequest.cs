using System.Collections.Generic;
using Reimaginate.DataHub.SharedModels.Core;


namespace Reimaginate.DataHub.SharedModels.Requests.Client;

public class GetDataHubEntitiesByAltKeyRequest : DataHubClientRequest<GetDataHubEntitiesByAltKeyResponse>
{
    public GetDataHubEntitiesByAltKeyRequest()
    {
        RequestType = nameof(GetDataHubEntitiesByAltKeyRequest);
    }

    public string EntityType { get; set; }

    public List<AlternateKey> AlternateKeys { get; set; } = new();
}