namespace Reimaginate.DataHub.SharedModels.Requests.Client;

public class GetJobsRequest : DataHubClientRequest<GetJobsResponse>
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