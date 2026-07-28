using System.Collections.Generic;
using Reimaginate.DataHub.SharedModels.Core;

namespace Reimaginate.DataHub.SharedModels.Requests.CLI;

public class GetEntitiesByAltKeyRequest : DataHubCLIRequest<GetEntitiesResponse>
{
    public GetEntitiesByAltKeyRequest()
    {
        RequestType = nameof(GetEntitiesByAltKeyRequest);
    }
  
    public List<AlternateKey> AlternateKeys { get; set; } = new();
    public int PageSize { get; set; } = 100;
    public string ContinuationToken { get; set; }
}