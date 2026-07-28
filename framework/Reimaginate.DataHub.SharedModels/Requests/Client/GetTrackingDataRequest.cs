namespace Reimaginate.DataHub.SharedModels.Requests.Client;

public class GetTrackingDataRequest : DataHubClientRequest<GetTrackingDataResponse>
{
    public GetTrackingDataRequest()
    {
        RequestType = nameof(GetTrackingDataRequest);
    }
    public string Select { get; set; }
    public string From { get; set; }
    public string WhereClause { get; set; }
    public string OrderBy { get; set; }
    public int PageSize { get; set; } = 500;
    public string ContinuationToken { get; set; }
    public bool GetTotalResultCount { get; set; } = false;
}