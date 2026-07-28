using Reimaginate.DataHub.SharedModels.Core;

namespace Reimaginate.DataHub.SharedModels.Requests.CLI;

public class DeleteJobRequest : DataHubCLIRequest<DeleteJobResponse>
{
    public DeleteJobRequest()
    {
        RequestType = nameof(DeleteJobRequest);
    }

    public string JobId { get; set; }
}