namespace Reimaginate.DataHub.SharedModels.Requests.Client;

public class DeleteJobRequest : DataHubClientRequest<DeleteJobResponse>
{
    public DeleteJobRequest()
    {
        RequestType = nameof(DeleteJobRequest);
    }

    public string JobId { get; set; }
}