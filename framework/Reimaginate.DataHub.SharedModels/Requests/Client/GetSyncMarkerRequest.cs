namespace Reimaginate.DataHub.SharedModels.Requests.Client;

public class GetSyncMarkerRequest : DataHubClientRequest<GetSyncMarkerResponse>
{
    public GetSyncMarkerRequest()
    {
        RequestType = nameof(GetSyncMarkerRequest);
    }
        
    public string DataSource { get; set; }
    public string AgentId { get; set; }
    public string DataHubEntityType { get; set; }
    public string DefaultValue { get; set; }
}