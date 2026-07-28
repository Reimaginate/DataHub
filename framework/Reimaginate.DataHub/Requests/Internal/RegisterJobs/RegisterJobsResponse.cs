using System.Collections.Generic;

namespace Reimaginate.DataHub.Requests.Internal.RegisterJobs;

public class RegisterJobsResponse
{
    public bool Success { get; set; }
    public string FailureReason { get; set; }
    public List<RegisterJobResult> Results { get; set; }
}