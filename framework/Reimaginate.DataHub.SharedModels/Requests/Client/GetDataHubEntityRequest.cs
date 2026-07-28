namespace Reimaginate.DataHub.SharedModels.Requests.Client;

public class GetDataHubEntityRequest : DataHubClientRequest<GetDataHubEntityResponse>
{
    public GetDataHubEntityRequest()
    {
        RequestType = nameof(GetDataHubEntityRequest);
    }

    public string EntityType { get; set; }
    public string EntityId { get; set; }
}