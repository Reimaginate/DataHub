using Reimaginate.DataHub.SharedModels.Core;

namespace Reimaginate.DataHub.SharedModels.Requests.CLI;

public class GetDataHubEntityTypeCountsRequest : DataHubCLIRequest<GetDataHubEntityTypeCountsResponse>
{
    public GetDataHubEntityTypeCountsRequest()
    {
        RequestType = nameof(GetDataHubEntityTypeCountsRequest);
    }
    public string WhereClause { get; set; }
}