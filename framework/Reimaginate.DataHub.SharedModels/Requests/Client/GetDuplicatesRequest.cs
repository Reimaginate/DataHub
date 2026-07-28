namespace Reimaginate.DataHub.SharedModels.Requests.Client;

public class GetDuplicatesRequest : DataHubClientRequest<GetDuplicatesResponse>
{
    public GetDuplicatesRequest()
    {
        RequestType = nameof(GetDuplicatesRequest);
    }
    public string Select { get; set; }
    public string Where { get; set; }
    public string OrderBy { get; set; }
    public int? PageSize { get; set; }
    public string ContinuationToken { get; set; }
    public bool GetTotalResultCount { get; set; }
}