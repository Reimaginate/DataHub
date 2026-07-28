using Reimaginate.DataHub.SharedModels.Core;

namespace Reimaginate.DataHub.SharedModels.Requests.CLI;

public class DeleteDuplicatesRequest : DataHubCLIRequest<DeleteDuplicatesResponse>
{
    public DeleteDuplicatesRequest()
    {
        RequestType = nameof(DeleteDuplicatesRequest);
    }

    public string Where { get; set; }
}