using Reimaginate.DataHub.SharedModels.Core;

namespace Reimaginate.DataHub.SharedModels.Requests.CLI;

public class GetJobsRequest : DataHubCLIRequest<GetJobsResponse>
{
    public GetJobsRequest()
    {
        RequestType = nameof(GetJobsRequest);
    }
    public string Select { get; set; }
    public string Where { get; set; }
    public string OrderBy { get; set; }
    public int? PageSize { get; set; }
    public string ContinuationToken { get; set; }
    public bool GetTotalResultCount { get; set; }
}