using System.Collections.Generic;

namespace Reimaginate.DataHub.SharedModels.Requests.Client;

public class UpdateUntrackedEntitiesResponse
{
    public bool Success { get; set; }
    public string FailureReason { get; set; }
    public List<UpdateUntrackedEntityResponse> Results { get; set; }
}