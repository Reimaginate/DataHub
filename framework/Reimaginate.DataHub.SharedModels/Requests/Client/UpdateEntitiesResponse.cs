using System.Collections.Generic;

namespace Reimaginate.DataHub.SharedModels.Requests.Client;

public class UpdateEntitiesResponse
{
    public bool Success { get; set; }
    public string FailureReason { get; set; }
    public List<UpdateEntityResponse> Results { get; set; }
}