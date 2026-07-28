namespace Reimaginate.DataHub.SharedModels.Requests.Client;

public class DeleteJobsRequest : DataHubClientRequest<DeleteJobsResponse>
{
    public DeleteJobsRequest()
    {
        RequestType = nameof(DeleteJobsRequest);
    }

    public string Where { get; set; }
}