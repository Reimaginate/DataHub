namespace Reimaginate.DataHub.SharedModels.Requests.Client;

public class GetDuplicateRequest : DataHubClientRequest<GetDuplicateResponse>
{
    public GetDuplicateRequest()
    {
        RequestType = nameof(GetDuplicateRequest);
    }
    public string Id { get; set; }
}