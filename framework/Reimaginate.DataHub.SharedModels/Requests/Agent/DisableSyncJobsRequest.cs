using Reimaginate.DataHub.SharedModels.Core;

namespace Reimaginate.DataHub.SharedModels.Requests.Agent;

public class DisableSyncJobsRequest : AgentRequest<DisableSyncJobsResponse>
{
    public DisableSyncJobsRequest()
    {
        RequestType = nameof(DisableSyncJobsRequest);
    }
}