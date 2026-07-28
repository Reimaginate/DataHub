using System.Collections.Generic;
using Reimaginate.DataHub.SharedModels.Core;

namespace Reimaginate.DataHub.SharedModels.Requests.Client;

public class MergeEntitiesResponse
{
    public bool Success { get; set; }
    public string FailureReason { get; set; }
    public List<MergeEntityResult> Results { get; set; } = new();
}