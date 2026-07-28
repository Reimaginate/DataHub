using System.Collections.Generic;

namespace Reimaginate.DataHub.SharedModels.Requests.Client;

public class GetLogEntriesByIdRequest : DataHubClientRequest<GetLogEntriesResponse>
{
    public GetLogEntriesByIdRequest()
    {
        RequestType = nameof(GetLogEntriesByIdRequest);
    }

    public string Select { get; set; }
    public List<string> Ids { get; set; } = new();
}