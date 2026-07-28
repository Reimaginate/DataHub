using Reimaginate.DataHub.SharedModels.Core;

namespace Reimaginate.DataHub.SharedModels.Requests.Agent;

public class MergeEntitiesResponse : AgentResponse
{
    public bool Success { get; set; }
    public string FailureReason { get; set; }
    public string JobId { get; set; }
}