using System.Collections.Generic;
using Reimaginate.DataHub.SharedModels.Core;

namespace Reimaginate.DataHub.SharedModels.Requests.CLI;

public class RebaseSourceEntitiesResponse
{
    public bool Success { get; set; }
    public string FailureReason { get; set; }
    public List<RebaseEntityTrackingResult> Results { get; set; } = new();
}