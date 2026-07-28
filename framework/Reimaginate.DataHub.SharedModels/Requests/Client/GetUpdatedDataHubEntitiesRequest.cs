using System;

namespace Reimaginate.DataHub.SharedModels.Requests.Client;

public class GetUpdatedDataHubEntitiesRequest : DataHubClientRequest<GetDataHubEntitiesResponse>
{

    public GetUpdatedDataHubEntitiesRequest()
    {
        RequestType = nameof(GetUpdatedDataHubEntitiesRequest);
    }

    public string EntityType { get; set; }
    public DateTimeOffset FromDateTime { get; set; }
    public DateTimeOffset? ToDateTime { get; set; }
    public string Select { get; set; }
    public int PageSize { get; set; } = 100;
    public string ContinuationToken { get; set; }
}