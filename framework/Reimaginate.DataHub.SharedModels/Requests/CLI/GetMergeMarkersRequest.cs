using Reimaginate.DataHub.SharedModels.Core;

namespace Reimaginate.DataHub.SharedModels.Requests.CLI;

public class GetMergeMarkersRequest : DataHubCLIRequest<GetMergeMarkersResponse>
{
    public GetMergeMarkersRequest()
    {
        RequestType = nameof(GetMergeMarkersRequest);
    }

    public string WhereClause { get; set; }
    public string OrderBy { get; set; }
    public int PageSize { get; set; } = 100;
    public string ContinuationToken { get; set; }
}