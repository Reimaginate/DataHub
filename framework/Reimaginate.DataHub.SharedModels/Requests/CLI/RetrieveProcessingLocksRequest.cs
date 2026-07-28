using Reimaginate.DataHub.SharedModels.Core;

namespace Reimaginate.DataHub.SharedModels.Requests.CLI;

public class RetrieveProcessingLocksRequest : DataHubCLIRequest<RetrieveProcessingLocksResponse>
{
    public RetrieveProcessingLocksRequest()
    {
        RequestType = nameof(RetrieveProcessingLocksRequest);
    }
}