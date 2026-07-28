namespace Reimaginate.DataHub.SharedModels.Requests.Client;

public class GetMergeMarkerRequest : DataHubClientRequest<GetMergeMarkerResponse>
{
    public GetMergeMarkerRequest()
    {
        RequestType = nameof(GetMergeMarkerRequest);
    }

    public string DataSource { get; set; }
    public string AgentId { get; set; }
    public string SourceEntityType { get; set; }
    public string DefaultValue { get; set; }
}