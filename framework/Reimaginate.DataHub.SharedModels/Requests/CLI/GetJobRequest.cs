using Reimaginate.DataHub.SharedModels.Core;

namespace Reimaginate.DataHub.SharedModels.Requests.CLI;

public class GetJobRequest : DataHubCLIRequest<GetJobResponse>
{
    public GetJobRequest()
    {
        RequestType = nameof(GetJobRequest);
    }
    public string JobId { get; set; }
}