using System.Collections.Generic;

namespace Reimaginate.DataHub.SharedModels.Requests.Client;

public class CreateEntitiesResponse
{
    public bool Success { get; set; }
    public string FailureReason { get; set; }
    public List<CreateEntityResponse> Results { get; set; }
}