using Reimaginate.DataHub.SharedModels.Core;

namespace Reimaginate.DataHub.SharedModels.Requests.CLI;

public class GetDuplicatesRequest : DataHubCLIRequest<GetDuplicatesResponse>
{
    public GetDuplicatesRequest()
    {
        RequestType = nameof(GetDuplicatesRequest);
    }
    public string Where { get; set; }
    public int? PageSize { get; set; }
    public string ContinuationToken { get; set; }
    public bool GetTotalResultCount { get; set; }
}
