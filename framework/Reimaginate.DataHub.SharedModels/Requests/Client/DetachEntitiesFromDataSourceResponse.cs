using System.Collections.Generic;

namespace Reimaginate.DataHub.SharedModels.Requests.Client;

public class DetachEntitiesFromDataSourceResponse
{
    public bool Success { get; set; }
    public string FailureReason { get; set; }
    public List<string> Failures { get; set; } = new();
}
