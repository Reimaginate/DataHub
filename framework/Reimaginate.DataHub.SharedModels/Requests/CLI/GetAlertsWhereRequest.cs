using Reimaginate.DataHub.SharedModels.Core;

namespace Reimaginate.DataHub.SharedModels.Requests.CLI;

public class GetAlertsWhereRequest : DataHubCLIRequest<GetAlertsResponse>
{
    public GetAlertsWhereRequest()
    {
        RequestType = nameof(GetAlertsWhereRequest);
    }
    public string Select { get; set; }
    public string WhereClause { get; set; }
    public string OrderBy { get; set; }
    public int PageSize { get; set; } = 100;
    public string ContinuationToken { get; set; }
    public bool GetTotalResultCount { get; set; }

}