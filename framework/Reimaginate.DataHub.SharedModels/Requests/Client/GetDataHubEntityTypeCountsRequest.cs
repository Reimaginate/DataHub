namespace Reimaginate.DataHub.SharedModels.Requests.Client;

public class GetDataHubEntityTypeCountsRequest : DataHubClientRequest<GetDataHubEntityTypeCountsResponse>
{
    public GetDataHubEntityTypeCountsRequest()
    {
        RequestType = nameof(GetDataHubEntityTypeCountsRequest);
    }
    public string WhereClause { get; set; }
}