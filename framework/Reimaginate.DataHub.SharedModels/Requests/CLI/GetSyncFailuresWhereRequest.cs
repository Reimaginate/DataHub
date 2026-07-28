using Reimaginate.DataHub.SharedModels.Core;

namespace Reimaginate.DataHub.SharedModels.Requests.CLI;

public class GetSyncFailuresWhereRequest : DataHubCLIRequest<GetSyncFailuresResponse>
{
    public GetSyncFailuresWhereRequest()
    {
        RequestType = nameof(GetSyncFailuresWhereRequest);
    }
    public string Select { get; set; }
    public string WhereClause { get; set; }
    public string OrderBy { get; set; }
    public int PageSize { get; set; } = 100;
    public string ContinuationToken { get; set; }
    public bool GetTotalResultCount { get; set; }
  
}