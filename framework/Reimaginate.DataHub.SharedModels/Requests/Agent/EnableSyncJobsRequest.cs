using Reimaginate.DataHub.SharedModels.Core;

namespace Reimaginate.DataHub.SharedModels.Requests.Agent;

public class EnableSyncJobsRequest : AgentRequest<EnableSyncJobsResponse>
{
    public EnableSyncJobsRequest()
    {
        RequestType = nameof(EnableSyncJobsRequest);
    }
}