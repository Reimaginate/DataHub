using System.Collections.Generic;

namespace Reimaginate.DataHub.SharedModels.Requests.Client;

public class PatchEntitiesResponse
{
    public bool Success { get; set; }
    public string FailureReason { get; set; }
    public List<PatchEntityResponse> Results { get; set; }
}