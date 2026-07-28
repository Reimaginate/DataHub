namespace Reimaginate.DataHub.SharedModels.Requests.Client;

public class GetDataHubEntityByAltKeyRequest : DataHubClientRequest<GetDataHubEntityByAltKeyResponse>
{
    public GetDataHubEntityByAltKeyRequest()
    {
        RequestType = nameof(GetDataHubEntityByAltKeyRequest);
    }

    public string EntityType { get; set; }
    public string Key { get; set; }
    public string Value { get; set; }
}