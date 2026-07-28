using System.Collections.Generic;
using Reimaginate.DataHub.SharedModels.Core.Models.Failures;

namespace Reimaginate.DataHub.SharedModels.Requests.Client;

public class PatchEntityResponse
{
    public bool Success { get; set; }
    public string FailureReason { get; set; }
    public PatchEntityRequest PatchRequest { get; set; }
    public List<PatchFailure> PatchFailures { get; set; }
}