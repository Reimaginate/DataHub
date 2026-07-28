using Reimaginate.DataHub.SharedModels.Core;

namespace Reimaginate.DataHub.SharedModels.Requests.CLI;

public class RetryJobRequest : DataHubCLIRequest<RetryJobResponse>
{
    public RetryJobRequest()
    {
        RequestType = nameof(RetryJobRequest);
    }
    public string JobId { get; set; }
}