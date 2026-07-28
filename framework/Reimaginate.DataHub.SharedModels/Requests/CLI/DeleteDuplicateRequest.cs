using Reimaginate.DataHub.SharedModels.Core;

namespace Reimaginate.DataHub.SharedModels.Requests.CLI;

public class DeleteDuplicateRequest : DataHubCLIRequest<DeleteDuplicateResponse>
{
    public DeleteDuplicateRequest()
    {
        RequestType = nameof(DeleteDuplicateRequest);
    }

    public string Id { get; set; }
}