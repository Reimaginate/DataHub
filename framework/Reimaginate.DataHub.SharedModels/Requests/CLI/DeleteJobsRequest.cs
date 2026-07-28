using Reimaginate.DataHub.SharedModels.Core;

namespace Reimaginate.DataHub.SharedModels.Requests.CLI;

public class DeleteJobsRequest : DataHubCLIRequest<DeleteJobsResponse>
{
    public DeleteJobsRequest()
    {
        RequestType = nameof(DeleteJobsRequest);
    }

    public string Where { get; set; }
}