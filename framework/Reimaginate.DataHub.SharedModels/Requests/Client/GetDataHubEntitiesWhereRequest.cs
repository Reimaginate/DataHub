namespace Reimaginate.DataHub.SharedModels.Requests.Client;

public class GetDataHubEntitiesWhereRequest : DataHubClientRequest<GetDataHubEntitiesResponse>
{
    public GetDataHubEntitiesWhereRequest()
    {
        RequestType = nameof(GetDataHubEntitiesWhereRequest);
    }
    public string Select { get; set; }
    public string From { get; set; }
    public string WhereClause { get; set; }
    public string OrderBy { get; set; }
    public int PageSize { get; set; } = 100;
    public string ContinuationToken { get; set; }
    public bool GetTotalResultCount { get; set; }
}