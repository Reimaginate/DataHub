using System.Collections.Generic;
using Reimaginate.DataHub.SharedModels.Core.Models.Failures;

namespace Reimaginate.DataHub.SharedModels.Requests.CLI;

public class PatchEntityResponse
{
    public bool Success { get; set; }
    public bool? Changed { get; set; }
    public string FailureReason { get; set; }
    public string DataSource { get; set; }
    public string EntityType { get; set; }
    public string EntityId { get; set; }
    public List<PatchFailure> PatchFailures { get; set; }
}
