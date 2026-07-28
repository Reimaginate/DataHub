using Reimaginate.DataHub.SharedModels.Core;

namespace Reimaginate.DataHub.SharedModels.Requests.CLI;

public class GetEntitiesWhereRequest : DataHubCLIRequest<GetEntitiesResponse>
{
    public GetEntitiesWhereRequest()
    {
        RequestType = nameof(GetEntitiesWhereRequest);
    }
    public string Select { get; set; }
    public string From { get; set; }
    public string WhereClause { get; set; }
    public string OrderBy { get; set; }
    public int PageSize { get; set; } = 100;
    public string ContinuationToken { get; set; }
    public bool GetTotalResultCount { get; set; }

}