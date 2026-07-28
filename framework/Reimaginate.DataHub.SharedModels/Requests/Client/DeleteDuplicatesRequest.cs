namespace Reimaginate.DataHub.SharedModels.Requests.Client;

public class DeleteDuplicatesRequest : DataHubClientRequest<DeleteDuplicatesResponse>
{
    public DeleteDuplicatesRequest()
    {
        RequestType = nameof(DeleteDuplicatesRequest);
    }

    public string Where { get; set; }
}