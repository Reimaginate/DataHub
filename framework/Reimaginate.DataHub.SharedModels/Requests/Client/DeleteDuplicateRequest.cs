namespace Reimaginate.DataHub.SharedModels.Requests.Client;

public class DeleteDuplicateRequest : DataHubClientRequest<DeleteDuplicateResponse>
{
    public DeleteDuplicateRequest()
    {
        RequestType = nameof(DeleteDuplicateRequest);
    }

    public string Id { get; set; }
}