namespace Reimaginate.DataHub.SharedModels.Requests.Client;

public class GetLogEntriesWhereRequest : DataHubClientRequest<GetLogEntriesResponse>
{
    public GetLogEntriesWhereRequest()
    {
        RequestType = nameof(GetLogEntriesWhereRequest);
    }
    public string Select { get; set; }
    public string WhereClause { get; set; }
    public string OrderBy { get; set; }
    public int PageSize { get; set; } = 100;
    public string ContinuationToken { get; set; }
    public bool GetTotalResultCount { get; set; }
  
}